using MetaQuotes.MT5CommonAPI;
using TestScalpingBackend.Services;
using TestScalpingBackend.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace TestScalpingBackend.TradeOperations
{
    public class TradeOperation
    {
        public MT5Operations mT5Operations = null!;
        private IHubContext<ProfitOutDealHub> _hubContext;
        private IServiceScopeFactory _serviceProvider;
        
        public TradeOperation(MT5Operations mT5Operations
        , IHubContext<ProfitOutDealHub> hubContext,
        IServiceScopeFactory serviceProvider
        )
        {
            this.mT5Operations = mT5Operations;
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
        }

        public async Task SendTrade(ulong Login, uint entry, ulong PositionID, uint action, string Symbol, double OUtDealProfit, long outdealtime, ulong DealId)
        {

            Console.WriteLine("DEAl come for operation " + entry + " " + PositionID + " " + action + " " + Symbol + " " + OUtDealProfit + " " + Login);

            // using var scope = _serviceProvider.CreateScope();
            // _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            CIMTDealArray DealsWithSameLogin = dealsArray(Login);
            CIMTDeal InDeal = null;
            CIMTDeal OutDeal = null;

            for (uint i = 0; i < DealsWithSameLogin.Total(); i++)
            {
                CIMTDeal item = DealsWithSameLogin.Next(i);
                DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(item.Time()).DateTime;
                if (item.PositionID() == PositionID && item.Symbol() == Symbol && item.Login() == Login && item.Entry() == 0 && (item.Action() == 0 || item.Action() == 1))
                {
                    InDeal = item;
                }

                if (item.Deal() == DealId)
                {
                    OutDeal = item;
                }

            }

            if (InDeal == null)
            {
                Console.WriteLine($"❗️InDeal not found for PositionID={PositionID}, Login={Login}");
                return;
            }
            if (OutDeal == null)
            {
                Console.WriteLine($"❗️OutDeal not found for DealId={DealId}");
                return;
            }

            // Calculate the profit bw the entry and exit
            var Profit = OUtDealProfit - InDeal.Profit();
            var OutTime = DateTimeOffset.FromUnixTimeSeconds(outdealtime).DateTime;
            var InTime = DateTimeOffset.FromUnixTimeSeconds(InDeal.Time()).DateTime;
            var timedifference = OutTime - InTime;
            Console.WriteLine("Time difference " + timedifference.TotalSeconds + " " + timedifference.TotalMinutes + " " + timedifference.TotalHours + " " + timedifference.TotalDays);
            Console.WriteLine("profit " + Profit);
            if (InDeal == null || OutDeal == null)
            {
                Console.WriteLine($"❗️InDeal or OutDeal is null ");
            }

            if (Profit > 0 && timedifference.TotalSeconds < 180 && InDeal != null && OutDeal != null)
            {
                string comment = $"Scalping Deduction #{OutDeal.Deal()}";
                var response = mT5Operations.UpdateCredit(Login, -Profit, 2, comment, out ulong dealid);

                if (response == MTRetCode.MT_RET_REQUEST_DONE)
                {
                    Console.WriteLine("Credit Updated");
                    await ProfitOutDeal(OutDeal, InDeal, OutTime, InTime);
                }
                else
                {
                    Console.WriteLine("Credit Not Updated");
                }
            }
            else
            {
                Console.WriteLine(" time difference is greater than 3 minutes");
            }

        }

        public async Task ProfitOutDeal(CIMTDeal Outdeal, CIMTDeal InDeal, DateTimeOffset outtime, DateTimeOffset intime)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ProfitOutDeals profitDeal = new ProfitOutDeals
            {
                DealId = Outdeal.Deal(),
                Profit = Outdeal.Profit(),
                Symbol = Outdeal.Symbol(),
                Login = Outdeal.Login(),
                ClosingTime = outtime.ToUniversalTime(),
                Entry = Outdeal.Entry(),
                Action = Outdeal.Action(),
                Volume = Outdeal.Volume(),
                PositionID = Outdeal.PositionID(),
                OpeningTime = intime.ToUniversalTime(),
                ProfitOut = Outdeal.Profit() - InDeal.Profit(),
                Comment = Outdeal.Comment()
            };

            await SendProfitOutDealsAsync(profitDeal);

            try
            {
                Console.WriteLine("Profit Deal Added in Database ");
                context.ProfitOutDeals.Add(profitDeal);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                foreach (var entry in ex.Entries)
                {
                    Console.WriteLine($"Error updating {entry.Entity.GetType().Name}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating the Profit Deals in the Database : {ex.Message}");
            }

        }

        public long DateTimeToUnixTime(DateTimeOffset dateTime)
        {
            return dateTime.ToUniversalTime().ToUnixTimeSeconds();
        }

        public CIMTDealArray dealsArray(ulong login)
        {
            ulong[] Logins = { login };
            long startTime = 0;
            var endTime = DateTimeOffset.UtcNow.AddDays(1);
            long EndTimeInUnix = DateTimeToUnixTime(endTime);
            var response = mT5Operations.GetDealsWithLoginAndSymbol(Logins, startTime, EndTimeInUnix, out CIMTDealArray deals);
            // Console.WriteLine("Response " + response);

            return deals;

        }

        public async Task SendProfitOutDealsAsync(ProfitOutDeals deals)
        {
            Console.WriteLine("Deal Send to the client ");
            await _hubContext.Clients.All.SendAsync("ReceiveProfitOutDeals", deals);
        }

    }

}


