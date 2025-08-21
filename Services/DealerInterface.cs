using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using TestScalpingBackend.TradeOperations;
using Microsoft.AspNetCore.SignalR;
using TestScalpingBackend.Models;

namespace TestScalpingBackend.Services
{
    public class DealSubscribe : CIMTDealSink
    {

        // public CIMTManagerAPI m_manager = null!;
        // public TradeOperation? tradeOperation;
        private int _creditProfit = 0;
        // private readonly IServiceScopeFactory _scopeFactory;
        // private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1000);

        public bool CreditProfit
        {
            get => Interlocked.CompareExchange(ref _creditProfit, 1, 1) == 1;
            set
            {
                Interlocked.Exchange(ref _creditProfit, value ? 1 : 0);
                BroadcastCreditProfit(value);
            }
        }

        private void BroadcastCreditProfit(bool currentValue)
        {
            _hubContext.Clients.All.SendAsync("CreditProfitChanged", currentValue);
        }

        // public int TimeDelay = 0;
        private IHubContext<ProfitOutDealHub> _hubContext;
        // private AppDbContext _context;
        private event EventHandler<NewDealDto> ProfitOutEvent;
        private readonly ILogger<DealSubscribe> _logger;
        public DealSubscribe(
         IHubContext<ProfitOutDealHub> hubContext,
         ILogger<DealSubscribe> logger

        //  IServiceScopeFactory scopeFactory
        )
        {
            // _hubContext = hubContext;
            // _scopeFactory = scopeFactory;
            _logger = logger;
            // Initialize();
            // if (result == MTRetCode.MT_RET_REQUEST_DONE && m_manager != null)
            // {
            // m_manager.DealSubscribe(this);
            // }
            // else
            // {
            //     Console.WriteLine($"Failed to initialize DealSubscribe: {result}");
            // }

            RegisterSink();
        }

        // public MTRetCode Initialize()
        // {
        //     MT5Connection connection = new MT5Connection();

        //     m_manager = connection.m_manager;

        //     if (m_manager == null)
        //     {
        //         Console.WriteLine("Manager NULL");
        //         return (MTRetCode.MT_RET_ERR_PARAMS);
        //     }

        // tradeOperation = new TradeOperation(new MT5Operations(m_manager), _hubContext, _scopeFactory);
        // Console.WriteLine("Manager initialized and now waiting for other deals to be added");

        // }

        public override void OnDealAdd(CIMTDeal deal)
        {
            // Console.WriteLine("DEAL ADDED");
            Console.WriteLine(deal.Print());

            ulong positionId = deal.PositionID();
            uint entry = deal.Entry();
            var Action = deal.Action();
            string Symbol = deal.Symbol();
            ulong Login = deal.Login();
            double Profit = deal.Profit();
            long outdealtime = deal.Time();
            ulong DealId = deal.Deal();


            var model = new NewDealDto()
            {
                PositionId = positionId,
                EntryType = entry,
                ActionType = Action,
                Symbol = Symbol,
                Profit = Profit,
                Login = Login,
                Time = DateTimeOffset.FromUnixTimeSeconds(outdealtime).DateTime,
                DealId = DealId
            };

            Console.WriteLine("Position ID " + positionId + " Entry " + entry + Action + " " + Symbol + " " + Profit + " " + Login);

            if (entry == 1 && (Action == 0 || Action == 1) && CreditProfit == true && Profit > 0)
            {
                Console.WriteLine("DEAL ADDED for background operation ");

                // if (tradeOperation == null)
                // {
                //     Console.WriteLine("❗️ TradeOperation is not initialized");
                //     return;
                // }

                // Task.Run(async () =>
                // {
                //     await _semaphore.WaitAsync();
                //     try
                //     {
                //         await tradeOperation.SendTrade(Login, entry, positionId, Action, Symbol, Profit, outdealtime, DealId);
                //     }
                //     catch (Exception ex)
                //     {
                //         Console.WriteLine($"Error in SendTrade: {ex}");
                //     }
                //     finally
                //     {
                //         _semaphore.Release();
                //     }
                // });

            }

        }

    }

}



