using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;
using Microsoft.AspNetCore.SignalR;
using TestScalpingBackend.Models;
using System.Text.Json;
using DictionaryExample;

namespace TestScalpingBackend.Services
{
    public class DealSubscribe : CIMTDealSink
    {

        private int _creditProfit = 0;
        public bool CreditProfit
        {
            get => Interlocked.CompareExchange(ref _creditProfit, 1, 1) == 1;
            set
            {
                Interlocked.Exchange(ref _creditProfit, value ? 1 : 0);
                BroadcastCreditProfit(value);
            }
        }
        private IHubContext<ProfitOutDealHub> _hubContext;
        private void BroadcastCreditProfit(bool currentValue)
        {
            _hubContext.Clients.All.SendAsync("CreditProfitChanged", currentValue);
        }
        public event Action<NewDealDto> ProfitOutEvent;
        private SymbolStore symbolStore;
        private readonly ILogger<DealSubscribe> _logger;

        public DealSubscribe(IHubContext<ProfitOutDealHub> hubContext, ILogger<DealSubscribe> logger, SymbolStore symbolStore)
        {
            _hubContext = hubContext;

            _logger = logger;
            RegisterSink();
            this.symbolStore = symbolStore;
        }

        public override void OnDealAdd(CIMTDeal deal)
        {

            ulong positionId = deal.PositionID();
            uint entry = deal.Entry();
            var Action = deal.Action();
            string Symbol = deal.Symbol();
            ulong Login = deal.Login();
            double Profit = deal.Profit();
            long outdealtime = deal.Time();
            ulong DealId = deal.Deal();

            var SymbolExistsInrules = symbolStore.ContainsSymbol(Symbol);


            if (entry == 1 && (Action == 0 || Action == 1) && CreditProfit == true && Profit > 0 && SymbolExistsInrules == true)
            {
                _logger.LogInformation("DEAL ADDED for background operation ");

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

                string Data = JsonSerializer.Serialize<NewDealDto>(model);

                _logger.LogInformation("DEAL ADDED for background operation " + Data);

                ProfitOutEvent?.Invoke(model);

            }

            if(SymbolExistsInrules == false)
            {
                _logger.LogInformation("Symbol  Not found In the rule  " + Symbol);
                CreditProfit = false;
            }

        }

    }

}



