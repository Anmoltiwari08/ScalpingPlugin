using Microsoft.AspNetCore.Mvc;
using TestScalpingBackend.Services;
using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using TestScalpingBackend.Models;
using System.Data;
using DictionaryExample;

namespace TestScalpingBackend.Helper
{

    public class ScalpingFindHelper
    {

        private MT5Operations mT5Operations;
        private ILogger<ScalpingFindHelper> logger;
        private AppDbContext appDbContext;
        private readonly CIMTManagerAPI m_manager;
        private SymbolStore symbolStore;
        private readonly IProfitDeductionQueue _profitDeductionQueue;

        public ScalpingFindHelper(MT5Connection mT5Connection, MT5Operations mT5Operations, ILogger<ScalpingFindHelper> logger, AppDbContext context, IProfitDeductionQueue profitDeductionQueue, SymbolStore symbolStore)
        {
            this.mT5Operations = mT5Operations;
            this.logger = logger;
            appDbContext = context;
            m_manager = mT5Connection.m_manager;
            _profitDeductionQueue = profitDeductionQueue;
            this.symbolStore = symbolStore;
        }

        public MTRetCode OutDealsWithoutScalpingDeductionFound(CIMTDealArray deals, out CIMTDealArray EntryOutDealsWithoutScalpingDeductionInGivenTime)
        {

            mT5Operations.CreateArrayOfDeals(out CIMTDealArray EntryOutDeals);
            mT5Operations.CreateArrayOfDeals(out CIMTDealArray EntryOutDealsWithoutScalpingDeduction);
            HashSet<ulong> ScalpingOutDealsCommentIds = new HashSet<ulong>();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);
                // var IsSymbolExist = symbolStore.ContainsSymbol(deal.Symbol());

                if (deal.Entry() == 1 && (deal.Action() == 0 || deal.Action() == 1) && deal.Profit() > 0
                // && IsSymbolExist
                )
                {
                    EntryOutDeals.AddCopy(deal);
                }

                if (deal.Action() == 2 && deal.Comment().Contains("Scalping Deduction"))
                {
                    string comment = deal.Comment();
                    int index = comment.IndexOf('#');
                    if (index >= 0 && index + 1 < comment.Length)
                    {
                        string DealId = comment[(index + 1)..].Trim();
                        if (ulong.TryParse(DealId, out ulong dealid))
                        {
                            ScalpingOutDealsCommentIds.Add(dealid);
                        }
                        // Console.WriteLine("Deal Id " + deal.Deal() + " with comment " + deal.Comment() + "Deal Id in comment is " + DealId);

                    }
                }

            }

            // Console.WriteLine("Deals with entry out and proft > 0 and either are buy or sell  deals are " + EntryOutDeals.Total());
            // Console.WriteLine("Deals with action 2 deducted scalping  are " + ScalpingOutDealsCommentIds.Count());

            for (uint i = 0, count = EntryOutDeals.Total(); i < count; i++)
            {
                CIMTDeal deal = EntryOutDeals.Next(i);

                if (!ScalpingOutDealsCommentIds.Contains(deal.Deal()))
                {
                    EntryOutDealsWithoutScalpingDeduction.AddCopy(deal);
                }

            }

            // Console.WriteLine("Deals without scalping deduction + whose deals are not qualify for deduction are " + EntryOutDealsWithoutScalpingDeduction.Total());

            deals?.Dispose();
            EntryOutDeals?.Dispose();

            EntryOutDealsWithoutScalpingDeductionInGivenTime = EntryOutDealsWithoutScalpingDeduction;

            return MTRetCode.MT_RET_OK;
        }

        public MTRetCode CheckDealsWithin3MinGap(CIMTDealArray deals, out CIMTDealArray GetDealsWithin3MinGap, out CIMTOrderArray GetEnrtryInOrderArray)
        {

            mT5Operations.CreateArrayOfDeals(out CIMTDealArray CreateDealArrayFor3MinGap);
            // mT5Operations.CreateArrayOfOrders(out CIMTOrderArray CrateEnrtryInOrderArray);

            HashSet<ulong> OrderID = new HashSet<ulong>();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);
                OrderID.Add(deal.PositionID());
            }

            Console.WriteLine("Total no of deals for entry in or order are " + OrderID.Count());

            Dictionary<ulong, long> ordersDictionary = new Dictionary<ulong, long>();

            mT5Operations.GetAllTheCloseOrdersByIDs(OrderID.ToArray(), out CIMTOrderArray orders);
            Console.WriteLine("All the orders requested from mt5  are " + orders.Total());

            for (uint i = 0, count = orders.Total(); i < count; i++)
            {
                CIMTOrder order = orders.Next(i);
                ordersDictionary.Add(order.Order(), order.TimeDone());
                // CrateEnrtryInOrderArray.AddCopy(order);
            }

            Console.WriteLine("Total no of deal id with time done is  " + ordersDictionary.Count());

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);
                if (ordersDictionary.TryGetValue(deal.PositionID(), out long timeDone))
                {

                    var OutTime = DateTimeOffset.FromUnixTimeSeconds(deal.Time()).DateTime;
                    var InTime = DateTimeOffset.FromUnixTimeSeconds(timeDone).DateTime;
                    var TimeDifference = (OutTime - InTime).TotalSeconds;
                    if (TimeDifference < 180)
                    {
                        logger.LogInformation($" Login ID is {deal.Login()} | Deal ID={deal.Deal()} | Position ID={deal.PositionID()} | Time Period={TimeDifference} seconds | Out Time={OutTime} and in time ={InTime} + profit is {deal.Profit()}");
                        CreateDealArrayFor3MinGap.AddCopy(deal);
                    }

                }
            }

            deals?.Dispose();
            // orders?.Dispose();
            GetDealsWithin3MinGap = CreateDealArrayFor3MinGap;
            GetEnrtryInOrderArray = orders;

            return MTRetCode.MT_RET_OK;
        }

        public MTRetCode CheckDealsInDatabase(CIMTDealArray deals, out CIMTDealArray GetDealsNotFoundInDatabaseHistory)
        {
            // Create output array
            mT5Operations.CreateArrayOfDeals(out CIMTDealArray createDealArray);

            // Step 1: Collect all deal IDs from the incoming deals
            List<ulong> incomingIds = new List<ulong>();
            // List<string> incomingDealIdsInComment = new List<string>();
            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                incomingIds.Add(deals.Next(i).Deal());
                // string comment  = $"Scalping Deduction #{deals.Next(i).Deal()}";
            }

            // Step 2: Query the database once for all those IDs
            var existingIds = appDbContext.ProfitOutDeals
                                          .Where(x => incomingIds.Contains(x.DealId))
                                          .Select(x => x.DealId)
                                          .ToHashSet();

            Console.WriteLine("Existing Deals ID in Database" + existingIds.Count);

            foreach (var deal in existingIds)
            {
                Console.WriteLine("Existing Deal id found in database is " + deal);
            }

            // Step 3: Add only those not found in DB to the new array
            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                var deal = deals.Next(i);
                if (!existingIds.Contains(deal.Deal()))
                {
                    createDealArray.AddCopy(deal);
                }

            }

            // Release incoming array
            deals?.Dispose();
            // Step 4: assign to out param
            GetDealsNotFoundInDatabaseHistory = createDealArray;

            return MTRetCode.MT_RET_OK; // success (adjust return as needed)
        }

        public MTRetCode CheckDealsInDealsHistory(CIMTDealArray deals, out CIMTDealArray FinalUnmatchedDeals, long Time)
        {
            mT5Operations.CreateArrayOfDeals(out CIMTDealArray createDealArray);
            Dictionary<ulong, HashSet<ulong>> dictionary = new();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                var deal = deals.Next(i);
                if (deal == null) continue; // defensive

                var login = deal.Login();
                var dealId = deal.Deal();

                if (dictionary.TryGetValue(login, out var dealsSet))
                {
                    dealsSet.Add(dealId);
                }
                else
                {
                    dictionary[login] = new HashSet<ulong> { dealId };
                }
            }

            ulong[] keyArray = dictionary.Keys.ToArray();
            logger.LogInformation("Total no of login id in dicionary are found is  " + keyArray.Length);

            long from = Time;
            long to = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

            mT5Operations.GetDealsWithLoginAndSymbol(keyArray, from, to, out CIMTDealArray dealArray);
            Console.WriteLine("Total no of deals for all that lgoins in   diecitonary are found is  " + dealArray.Total());

            FindWithScalpingDeduction(dealArray, out CIMTDealArray GetDealsWithScalpingDeduction);
            Console.WriteLine("Total no of deals with scalping deduction" + GetDealsWithScalpingDeduction.Total());

            HashSet<ulong> ScalpingOutDealsCommentIds = new HashSet<ulong>();

            for (uint i = 0, count = GetDealsWithScalpingDeduction.Total(); i < count; i++)
            {
                var deal = GetDealsWithScalpingDeduction.Next(i);
                if (deal == null) continue; // defensive

                var comment = deal.Comment();
                if (string.IsNullOrEmpty(comment)) continue;

                var parts = comment.Split('#');
                if (parts.Length > 1 && ulong.TryParse(parts[1].Trim(), out var dealId))
                {
                    ScalpingOutDealsCommentIds.Add(dealId);
                }
                // Console.WriteLine("tota deals found for all logins with Deal Id " + deal.Deal() + " with comment " + deal.Comment() + "Deal Id in comment is " + parts[1]);
            }

            Console.WriteLine("Total no of deals ids with scalping deduction" + ScalpingOutDealsCommentIds.Count);

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                var deal = deals.Next(i);
                if (!ScalpingOutDealsCommentIds.Contains(deal.Deal()))
                {
                    Console.WriteLine("added the deal in final deals because it is not present in ScalpingOutDealsCommentIds " + deal.Deal());
                    createDealArray.AddCopy(deal);
                }

            }

            deals?.Dispose();
            dealArray?.Dispose();
            GetDealsWithScalpingDeduction?.Dispose();

            FinalUnmatchedDeals = createDealArray;
            return MTRetCode.MT_RET_OK;

        }

        public MTRetCode FindWithScalpingDeduction(CIMTDealArray deals, out CIMTDealArray GetDealsWithScalpingDeduction)
        {
            mT5Operations.CreateArrayOfDeals(out CIMTDealArray createDealArray);

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                var deal = deals.Next(i);
                if (deal.Comment().Contains("Scalping Deduction"))
                {
                    createDealArray.AddCopy(deal);
                }
            }

            deals?.Dispose();

            GetDealsWithScalpingDeduction = createDealArray;

            return MTRetCode.MT_RET_OK;
        }

        public List<ProfitOutDeals> UnmatchedDealModal(CIMTDealArray deals, CIMTOrderArray orders)
        {

            Dictionary<ulong, long> ordersIds = new Dictionary<ulong, long>();

            for (uint i = 0, count = orders.Total(); i < count; i++)
            {
                var order = orders.Next(i);
                ordersIds.Add(order.Order(), order.TimeDone());
            }

            var unmatchedDeals = new List<ProfitOutDeals>();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                var deal = deals.Next(i);
                if (ordersIds.TryGetValue(deal.PositionID(), out long timeDone))
                {
                    var OpeningTime = DateTimeOffset.FromUnixTimeSeconds(deal.Time()).DateTime;
                    var ClosingTime = DateTimeOffset.FromUnixTimeSeconds(timeDone).DateTime;
                    var data = new ProfitOutDeals()
                    {
                        DealId = deal.Deal(),
                        Symbol = deal.Symbol(),
                        Login = deal.Login(),
                        Comment = deal.Comment(),
                        OpeningTime = OpeningTime,
                        ClosingTime = ClosingTime,
                        PositionID = deal.PositionID(),
                        Entry = deal.Entry(),
                        Action = deal.Action(),
                        ProfitOut = deal.Profit(),
                        TimeDifferenceInSeconds = (OpeningTime - ClosingTime).TotalSeconds.ToString()
                    };

                    unmatchedDeals.Add(data);
                }
            }

            deals?.Dispose();
            orders?.Dispose();

            return unmatchedDeals;

        }

    }

}

        
