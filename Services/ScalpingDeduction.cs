using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using TestScalpingBackend.Models;
namespace TestScalpingBackend.Services;

using MetaQuotes.MT5CommonAPI;
using Microsoft.Extensions.Logging;

public class ScalpingDeduction
{

    private MT5Operations mT5Operations;
    private IHubContext<ProfitOutDealHub> _hubContext;
    private ILogger<ScalpingDeduction> logger;
    private AppDbContext _context;

    public ScalpingDeduction(DealSubscribe dealSubscribe, MT5Operations mT5Operations, IHubContext<ProfitOutDealHub> hubContext, ILogger<ScalpingDeduction> logger, AppDbContext context)
    {
        this.mT5Operations = mT5Operations;
        this.logger = logger;
        _hubContext = hubContext;
        _context = context;
        dealSubscribe.ProfitOutEvent += ProfitDeduction;
    }

    public void ProfitDeduction(NewDealDto newDealDto)
    {

        var PositionId = newDealDto.PositionId;
        var OutDealId = newDealDto.DealId;
        var OutProfit = newDealDto.Profit;

        var InOrder = mT5Operations.GetOrderByID(PositionId);

        if (InOrder == null)
        {
            logger.LogError("No order for position {PositionId} was found. OutDealId={OutDealId}, OutProfit={OutProfit}, Login={Login}. So profit out will not be made.", PositionId, OutDealId, OutProfit, newDealDto.Login);
            return;
        }

        var InDealTime = InOrder.TimeDone();
        var InDealDateTime = DateTimeOffset.FromUnixTimeSeconds(InDealTime).DateTime;

        InOrder?.Release();

        DeductProfit(InDealDateTime, newDealDto.Time, OutProfit, newDealDto.Login, $"Scalping Deduction #{OutDealId}", OutDealId);

    }

    public void DeductProfit(DateTime InTime, DateTime OutTime, double Profit, ulong Login, string comment, ulong outDealId)
    {

        var CalculateDifferenceOfTime = OutTime - InTime;

        if (CalculateDifferenceOfTime.TotalMinutes < 3 && Profit > 0)
        {

            // var UpdateCredit = mT5Operations.UpdateCredit(Login, -Profit, 2, comment, out ulong dealid);

            // if (UpdateCredit == MTRetCode.MT_RET_REQUEST_DONE)
            // {
            //     logger.LogInformation($"Credit Updated Profit {Profit} dealid={outDealId} Login={Login} OutTime={OutTime} InTime={InTime} comment={comment}");
            //     // PositionUpdate(outDealId, OutTime, InTime);
            //     _ = Task.Run( () => PositionUpdate(outDealId, OutTime, InTime));

            // }
            // else
            // {
            //    logger.LogError($"Credit Not Updated Profit {Profit} Outdealid={outDealId} Login={Login} OutTime={OutTime} InTime={InTime} comment={comment} ErrorCode={UpdateCredit}");
            // }

        }
        
    }

    public async Task PositionUpdate(ulong OutDealId, DateTime OutDealTime, DateTime InDealTime)
    {
        var Outdeal = mT5Operations.GetDealByID(OutDealId);

        ProfitOutDeals profitDeal = new ProfitOutDeals
        {
            DealId = Outdeal.Deal(),
            Symbol = Outdeal.Symbol(),
            Login = Outdeal.Login(),
            ClosingTime = OutDealTime,
            Entry = Outdeal.Entry(),
            Action = Outdeal.Action(),
            Volume = Outdeal.Volume(),
            PositionID = Outdeal.PositionID(),
            OpeningTime = InDealTime,
            ProfitOut = Outdeal.Profit(),
            Comment = Outdeal.Comment()
        };
 
        await SendProfitOutDealsAsync(profitDeal);

        try
        {
            Console.WriteLine("Profit Deal Added in Database ");
            _context.ProfitOutDeals.Add(profitDeal);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            foreach (var entry in ex.Entries)
            {
                logger.LogError($"Error updating {entry.Entity.GetType().Name}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error updating the Profit Deals in the Database : {ex.Message}");
        }

    }

    public async Task SendProfitOutDealsAsync(ProfitOutDeals deals)
    {
        Console.WriteLine("Deal Send to the client ");
        await _hubContext.Clients.All.SendAsync("ReceiveProfitOutDeals", deals);
    }

}

