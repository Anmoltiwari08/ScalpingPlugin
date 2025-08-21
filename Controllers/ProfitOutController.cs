using Microsoft.AspNetCore.Mvc;
using TestScalpingBackend.Services;
using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using TestScalpingBackend.Models;
using Microsoft.AspNetCore.SignalR;
using TestScalpingBackend.TradeOperations;

namespace TestScalpingBackend.Controllers
{

    [ApiController]
    [Route("/api/v1/[controller]")]
    public class ProfitOutController : ControllerBase
    {

        private readonly DealSubscribe _dealSubscribe;
        private readonly MT5Operations mT5Operations;
        private readonly CIMTManagerAPI m_manager;
        private TradeOperation tradeOperations;
        private AppDbContext _context;
        private ILogger<ProfitOutController> logger;

        public ProfitOutController(MT5Connection mT5Connection, IHubContext<ProfitOutDealHub> hubContext, AppDbContext context, IServiceScopeFactory scopeFactory, ILogger<ProfitOutController> logger , TradeOperation tradeOperations)
        {
            // _dealSubscribe = dealSubscribe;
            _context = context;
            m_manager = mT5Connection.m_manager;
            this.tradeOperations = tradeOperations;
            // this.mT5Operations = new MT5Operations(dealSubscribe.m_manager);
            // m_manager = dealSubscribe.m_manager;
            // tradeOperations = new TradeOperation(mT5Operations, hubContext, scopeFactory);
            this.logger = logger;
        }
        
        [HttpPost("ProfitDeductionByTimeWindow")]
        public async Task<IActionResult> ProfitDeduction([FromBody] ProfitRemovalInTimeRange requestModel)
        {

            if (!ModelState.IsValid)
            {
                Console.WriteLine("Rquest Model is invalid ");
                return BadRequest("Rquest Model is invalid " + ModelState);
            }

            CIMTDealArray accounts = m_manager.DealCreateArray();
            string mask = "*";
            Console.WriteLine("From " + requestModel.StartTime + " To " + requestModel.EndTime);
            Console.WriteLine("From " + requestModel.StartTime.ToLocalTime() + " To " + requestModel.EndTime.ToLocalTime());
            long from = requestModel.StartTime.ToUniversalTime().ToUnixTimeSeconds();
            long to = requestModel.EndTime.ToUniversalTime().ToUnixTimeSeconds();

            mT5Operations.GetAllDealsByGroupInSpecifiedTime(ref accounts, from, to, mask);
            Console.WriteLine("Total " + accounts.Total());

            if (accounts.Total() == 0)
            {
                return Ok(new { message = $"No Deals Found in the Given time Range {requestModel.StartTime} to {requestModel.EndTime} " });
            }

            CIMTDealArray INDEals = GetDealsEntryIN(accounts);

            CIMTDealArray DealsWithScalpingPluginComment = GetDealsWithComment(accounts);

            accounts = GetDealsEntryOut(accounts);

            CIMTDealArray accountsWithTimeDifference = await RemoveProfitFromDealsAccounts(accounts, INDEals);

            CIMTDealArray finalMatchedDeals = FilterDealsBasedOnComment(accountsWithTimeDifference, DealsWithScalpingPluginComment);

            Console.WriteLine("finalMatchedDeals " + finalMatchedDeals.Total());

            CIMTDealArray unmatcheddeals = GetUnmatchedDeals(accountsWithTimeDifference, finalMatchedDeals);
            // RemoveProfitFromDeals(unmatcheddeals);

            List<NewDealDto> result = new List<NewDealDto>();

            for (uint i = 0; i < unmatcheddeals.Total(); i++)
            {
                CIMTDeal deal = unmatcheddeals.Next(i);
                if (deal != null)
                {
                    result.Add(new NewDealDto
                    {
                        DealId = deal.Deal(),
                        Profit = deal.Profit(),
                        Symbol = deal.Symbol(),
                        Login = deal.Login(),
                        PositionId = deal.PositionID(),
                        EntryType = deal.Entry(),
                        ActionType = deal.Action(),
                        Time = DateTimeOffset.FromUnixTimeSeconds(deal.Time())
                    });
                }
            }

            double totalProfit = result.Sum(x => x.Profit);

            // You can include it in the response like this:
            return Ok(new { Deals = result, TotalProfit = totalProfit });

        }
      
        private CIMTDealArray GetUnmatchedDeals(CIMTDealArray accountsWithTimeDifference, CIMTDealArray finalMatchedDeals)
        {
            // var logger = _loggerFactory.CreateLogger("DealFiltering");

            logger.LogInformation("Starting GetUnmatchedDeals...");

            // Create a hash set of matched deal IDs for quick lookup
            var matchedDealIds = new HashSet<ulong>();
            for (uint i = 0; i < finalMatchedDeals.Total(); i++)
            {
                matchedDealIds.Add(finalMatchedDeals.Next(i).Deal());
            }

            CIMTDealArray unmatchedDeals = m_manager.DealCreateArray();

            for (uint i = 0; i < accountsWithTimeDifference.Total(); i++)
            {
                var deal = accountsWithTimeDifference.Next(i);
                if (!matchedDealIds.Contains(deal.Deal()))
                {
                    unmatchedDeals.Add(deal);
                    logger.LogInformation("❌ Unmatched Deal ID={DealId} | Comment='{Comment}'", deal.Deal(), deal.Comment());
                }
            }

            logger.LogInformation("Finished filtering unmatched deals. Total unmatched deals: {Count}", unmatchedDeals.Total());

            return unmatchedDeals;
        }

        private CIMTDealArray FilterDealsBasedOnComment(CIMTDealArray entryOutDeals, CIMTDealArray commentDeals)
        {
            // var logger = _loggerFactory.CreateLogger("DealFiltering");

            logger.LogInformation("Starting FilterDealsBasedOnComment...");

            // Build a dictionary of all commentDeals for fast lookup
            var commentList = new List<(string Comment, ulong DealId)>();

            for (uint i = 0; i < commentDeals.Total(); i++)
            {
                var commentDeal = commentDeals.Next(i);
                commentList.Add((commentDeal.Comment(), commentDeal.Deal()));
            }

            CIMTDealArray matchedDeals = m_manager.DealCreateArray();

            for (uint i = 0; i < entryOutDeals.Total(); i++)
            {
                var entryOutDeal = entryOutDeals.Next(i);
                var entryOutDealIdStr = entryOutDeal.Deal().ToString();
                var entryOutComment = entryOutDeal.Comment();

                var match = commentList.FirstOrDefault(c => c.Comment.Contains(entryOutDealIdStr));
                if (!string.IsNullOrEmpty(match.Comment))
                {
                    matchedDeals.Add(entryOutDeal);
                    logger.LogInformation("✅ Matched Deal ID={DealId} | OutDeal Comment='{OutComment}' | Matched Comment='{MatchedComment}'",
                        entryOutDealIdStr, entryOutComment, match.Comment);
                }
            }

            logger.LogInformation("Finished filtering. Total matched deals: {Count}", matchedDeals.Total());

            return matchedDeals;
        }

        public CIMTDealArray GetDealsWithComment(CIMTDealArray deals)
        {
            CIMTDealArray DealsWithScalpingPluginComment = m_manager.DealCreateArray();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);
                if (deal.Comment().Contains("Scalping Deduction"))
                {
                    DealsWithScalpingPluginComment.Add(deal);
                }
            }
            Console.WriteLine("DealsWithScalpingPluginComment " + DealsWithScalpingPluginComment.Total());

            return DealsWithScalpingPluginComment;

        }

        // [HttpGet("ProfitDeductionBy")]
        // public async Task<IActionResult> Profit()
        // {

        //     DateTimeOffset from = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 20, 0, 0, 0, new TimeSpan(5, 30, 0));
        //     DateTimeOffset to = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 21, 0, 0, 0, new TimeSpan(5, 30, 0));

        //     Console.WriteLine("From " + from + " To " + to);
        //     long FromTimeStamp = from.ToUnixTimeSeconds();
        //     long ToTimeStamp = to.ToUnixTimeSeconds();
        //     Console.WriteLine("From TimeStamp " + FromTimeStamp + " To TimeStamp " + ToTimeStamp);

        //     CIMTDealArray deals = m_manager.DealCreateArray();

        //     var AllDealsWithScalpingComment = mT5Operations.GetAllDealsByGroupInSpecifiedTime(ref deals, FromTimeStamp, ToTimeStamp, "*");

        //     Console.WriteLine(deals.Total());
        //     deals = GetScalpingDeals(deals);
        //     Console.WriteLine(deals.Total());

        //     List<DealDto> response = new List<DealDto>();

        //     for (uint i = 0; i < deals.Total(); i++)
        //     {
        //         CIMTDeal deal = deals.Next(i);

        //         var Login = deal.Login();
        //         var Entry = deal.Entry();
        //         var DealId = deal.Deal();
        //         var Action = deal.Action();
        //         var TimeStamp = deal.Time();
        //         var Profit = deal.Profit();

        //         DateTimeOffset convertedTime = DateTimeOffset.FromUnixTimeSeconds(TimeStamp);
        //         DateTimeOffset currentLocalTime = DateTimeOffset.Now.ToLocalTime();

        //         // var ProfitPositive = -Profit;

        //         mT5Operations.UpdateCredit(Login, -Profit, 2, "Scalping Addition", out ulong dealid);

        //         var dealDto = new DealDto
        //         {
        //             Login = Login,
        //             Profit = Profit,
        //             Added = -Profit,
        //             Entry = Entry,
        //             DealId = DealId,
        //             Action = Action,
        //             TimeStamp = TimeStamp,
        //             ConvertedTime = convertedTime,
        //             CurrentLocalTime = currentLocalTime,
        //             BalanceOperationDealId = dealid
        //         };

        //         response.Add(dealDto);

        //     }

        //     return Ok(response);

        // }

        // [HttpGet("ProfitDeductionBy")]
        // public async Task<IActionResult> ProfitDeduct()
        // {

        //      ulong positionId = 
        //         uint entry = 
        //         var Action = 
        //         string Symbol = 
        //         ulong Login = 
        //         double Profit = 
        //         long outdealtime = 
        //         var date =
        //         ulong DealId = 

        //         await tradeOperations.SendTrade(Login, entry, positionId, Action, Symbol, Profit, outdealtime, DealId);



        // }

        private CIMTDealArray GetDealsEntryOut(CIMTDealArray deals)
        {
            CIMTDealArray EntryOutDeals = m_manager.DealCreateArray();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);
                if (deal.Entry() == 1 && deal.PositionID() != 0 && (deal.Action() == 0 || deal.Action() == 1) && deal.Profit() > 0)
                {
                    EntryOutDeals.Add(deal);
                }
            }

            return EntryOutDeals;

        }

        private CIMTDealArray GetScalpingDeals(CIMTDealArray deals)
        {
            CIMTDealArray EntryOutDeals = m_manager.DealCreateArray();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);
                if (deal.Action() == 2 && deal.Comment().Contains("Scalping") && deal.Entry() == 0)
                {
                    EntryOutDeals.Add(deal);
                }
                // Console.WriteLine(deal.Comment() + " " + deal.Action() + " " + deal.Entry() );
            }

            return EntryOutDeals;

        }

        private CIMTDealArray GetDealsThatNotExistOnDatabase(CIMTDealArray deals)
        {

            List<ulong> incomingDealIds = new List<ulong>();
            Dictionary<ulong, CIMTDeal> dealMap = new();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);
                ulong dealId = deal.Deal();
                incomingDealIds.Add(dealId);
                dealMap[dealId] = deal;
            }

            Console.WriteLine("Incoming Deals " + incomingDealIds.Count);
            HashSet<ulong> existingDealIds = _context.ProfitOutDeals
                .Where(x => incomingDealIds.Contains(x.DealId))
                .Select(x => x.DealId)
                .ToHashSet();

            Console.WriteLine("Existing Deals ID in Database" + existingDealIds.Count);
            CIMTDealArray newDealsOnly = m_manager.DealCreateArray();

            foreach (var dealId in incomingDealIds)
            {
                if (!existingDealIds.Contains(dealId))
                {
                    newDealsOnly.Add(dealMap[dealId]);
                }
            }
            Console.WriteLine("New Deals " + newDealsOnly.Total());

            return newDealsOnly;
        }

        [NonAction]
        private async Task<CIMTDealArray> RemoveProfitFromDealsAccounts(CIMTDealArray deals, CIMTDealArray INDeals)
        {

            deals = GetDealsThatNotExistOnDatabase(deals);

            Console.WriteLine("Deals with entry out  " + deals.Total());
            Console.WriteLine("Deals with entry in  " + INDeals.Total());

            CIMTDealArray dealsWithTimeDifference = DealsOutWithTimeDifference(deals, INDeals, out List<DateTimeOffset> InDeals, out List<double> InDealProfit);

            Console.WriteLine("Deals with time difference less than 3 minutes are  " + dealsWithTimeDifference.Total());


            // int index = 0;
            // for (uint i = 0, count = dealsWithTimeDifference.Total(); i < count; i++)
            // {

            //     CIMTDeal deal = deals.Next(i);

            // if (existingDealIds.Contains(deal.Deal()))
            // {
            //     index++;
            //     continue;
            // }

            // ulong positionId = deal.PositionID();
            // uint entry = deal.Entry();
            // var Action = deal.Action();
            // string Symbol = deal.Symbol();
            // ulong Login = deal.Login();
            // double Profit = deal.Profit();
            // long outdealtime = deal.Time();
            // var date = DateTimeOffset.FromUnixTimeSeconds(outdealtime);
            // ulong DealId = deal.Deal();
            // ulong comment = deal.Commen

            // await tradeOperations.SendTrade(Login, entry, positionId, Action, Symbol, Profit, outdealtime, DealId);
            // string comment = $"Scalping Deduction #{deal.Deal()}";

            // var response = mT5Operations.UpdateCredit(Login, -Profit, 2, comment, out ulong dealid);

            // if (response == MTRetCode.MT_RET_REQUEST_DONE)
            // {
            //     Console.WriteLine("Credit Updated" + Profit + " " + dealid);

            //     ProfitOutDeals profitDeal = new ProfitOutDeals
            //     {
            //         DealId = deal.Deal(),
            //         Profit = deal.Profit(),
            //         Symbol = deal.Symbol(),
            //         Login = deal.Login(),
            //         ClosingTime = DateTimeOffset.FromUnixTimeSeconds(deal.Time()).ToUniversalTime(),
            //         Entry = deal.Entry(),
            //         Action = deal.Action(),
            //         Volume = deal.Volume(),
            //         PositionID = deal.PositionID(),
            //         OpeningTime = InDeals[index].ToUniversalTime(),
            //         ProfitOut = deal.Profit() - InDealProfit[index],
            //         Comment = deal.Comment()
            //     };

            //     _context.ProfitOutDeals.Add(profitDeal);
            //     await _context.SaveChangesAsync();
            // }

            // index++;
            // }

            return dealsWithTimeDifference;

        }

        public void RemoveProfitFromDeals(CIMTDealArray deals)
        {

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);

                ulong positionId = deal.PositionID();
                uint entry = deal.Entry();
                var Action = deal.Action();
                string Symbol = deal.Symbol();
                ulong Login = deal.Login();
                double Profit = deal.Profit();
                long outdealtime = deal.Time();
                var date = DateTimeOffset.FromUnixTimeSeconds(outdealtime);
                ulong DealId = deal.Deal();

                string comment = $"Scalping Deduction #{deal.Deal()}";

                var response = mT5Operations.UpdateCredit(Login, -Profit, 2, comment, out ulong dealid);

                if (response == MTRetCode.MT_RET_REQUEST_DONE)
                {
                    Console.WriteLine("Credit Updated" + Profit + " " + dealid);

                }



            }

        }

        private CIMTDealArray GetDealsEntryIN(CIMTDealArray deals)
        {

            CIMTDealArray EntryINDeals = m_manager.DealCreateArray();

            for (uint i = 0, count = deals.Total(); i < count; i++)
            {
                CIMTDeal deal = deals.Next(i);
                if (deal.Entry() == 0 && deal.PositionID() != 0 && (deal.Action() == 0 || deal.Action() == 1))
                {
                    EntryINDeals.Add(deal);
                }
            }

            return EntryINDeals;

        }

        private CIMTDealArray DealsOutWithTimeDifference(CIMTDealArray outDeals, CIMTDealArray inDeals, out List<DateTimeOffset> InDeals, out List<double> InDealProfit)
        {
            CIMTDealArray result = m_manager.DealCreateArray();

            List<DateTimeOffset> InDealsList = new List<DateTimeOffset>();
            List<double> InDealProfitList = new List<double>();

            var inDealMap = new Dictionary<ulong, CIMTDeal>();

            for (uint i = 0; i < inDeals.Total(); i++)
            {
                CIMTDeal inDeal = inDeals.Next(i);

                var posId = inDeal.PositionID();

                inDealMap[posId] = inDeal;

            }

            for (uint i = 0; i < outDeals.Total(); i++)
            {
                CIMTDeal OutDeal = outDeals.Next(i);
                var posId = OutDeal.PositionID();

                if (inDealMap.TryGetValue(posId, out var INDEAL))
                {

                    var outTime = DateTimeOffset.FromUnixTimeSeconds(OutDeal.Time()).DateTime;
                    var inTime = DateTimeOffset.FromUnixTimeSeconds(INDEAL.Time()).DateTime;

                    var TimeDifference = (outTime - inTime).TotalSeconds;

                    if (TimeDifference <= 180)
                    {
                        Console.WriteLine("Out Deal Id " + OutDeal.Deal() + " Out Time " + outTime + " In Time " + inTime + " Time Difference " + TimeDifference);
                        result.Add(OutDeal);
                        InDealProfitList.Add(INDEAL.Profit());
                        InDealsList.Add(inTime);
                    }

                }

            }

            InDealProfit = InDealProfitList;
            InDeals = InDealsList;
            return result;
        }


    }

}


