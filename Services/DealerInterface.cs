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
        private void BroadcastCreditProfit(bool currentValue)
        {
            _hubContext.Clients.All.SendAsync("CreditProfitChanged", currentValue);
        }
        private SymbolStore symbolStore;
        private readonly ILogger<DealSubscribe> _logger;
        private IHubContext<ProfitOutDealHub> _hubContext;
        private readonly IDealQueue _dealQueue;

        public DealSubscribe(IHubContext<ProfitOutDealHub> hubContext, ILogger<DealSubscribe> logger, SymbolStore symbolStore, IDealQueue dealQueue)
        {
            _hubContext = hubContext;
            _logger = logger;
            RegisterSink();
            this.symbolStore = symbolStore;
            _dealQueue = dealQueue;
        }

        // public override async void OnDealAdd(CIMTDeal deal)
        // {

        //     ulong positionId = deal.PositionID();
        //     uint entry = deal.Entry();
        //     uint Action = deal.Action();
        //     string Symbol = deal.Symbol();
        //     ulong Login = deal.Login();
        //     double Profit = deal.Profit();
        //     long outdealtime = deal.Time();
        //     ulong DealId = deal.Deal();
        //     ulong Volume = deal.Volume();

        //     var model = new NewDealDto()
        //     {
        //         PositionId = positionId,
        //         EntryType = entry,
        //         ActionType = Action,
        //         Symbol = Symbol,
        //         Profit = Profit,
        //         Login = Login,
        //         Time = DateTimeOffset.FromUnixTimeSeconds(outdealtime).DateTime,
        //         DealId = DealId,
        //         Volume = Volume
        //     };

        //     try
        //     {
        //         try
        //         {
        //             var SymbolExistsInrules = symbolStore.ContainsSymbol(Symbol);

        //             if (entry == 1 && (Action == 0 || Action == 1) && Profit > 0
        //             && CreditProfit == true && SymbolExistsInrules == true
        //              )
        //             {

        //                 try
        //                 {
        //                     await _dealQueue.EnqueueAsync(model);
        //                 }
        //                 catch (Exception ex)
        //                 {
        //                     var json = JsonSerializer.Serialize(model);
        //                     _logger.LogError(ex, "Failed to enqueue deal {Json}", json);
        //                 }

        //             }

        //         }
        //         catch (Exception ex)
        //         {
        //             var json = JsonSerializer.Serialize(model);
        //             _logger.LogError(ex,
        //             "Error in processing Deal data {json}", json
        //             );

        //         }

        //     }
        //     catch (Exception ex)
        //     {
        //         var json = JsonSerializer.Serialize(model);
        //         _logger.LogError(ex,
        //         "Error processing deal data : {json} ", json);
        //     }
        //     finally
        //     {
        //         deal?.Release();
        //     }

        // }

    }

}

