using Microsoft.AspNetCore.SignalR;
using TestScalpingBackend.Models;
using TestScalpingBackend.Services;
using System.Text.Json;
using MetaQuotes.MT5CommonAPI;

public class ScalpingDeduction
{
    private readonly MT5Operations _mT5Operations;
    private readonly IHubContext<ProfitOutDealHub> _hubContext;
    private readonly ILogger<ScalpingDeduction> _logger;
    private readonly AppDbContext _context;
    private readonly IProfitDeductionQueue _profitDeductionQueue;

    public ScalpingDeduction(MT5Operations mT5Operations, IHubContext<ProfitOutDealHub> hubContext, ILogger<ScalpingDeduction> logger, AppDbContext context, IProfitDeductionQueue profitDeductionQueue)
    {
        _mT5Operations = mT5Operations;
        _hubContext = hubContext;
        _logger = logger;
        _profitDeductionQueue = profitDeductionQueue;
        _context = context;
    }

    public async Task ProfitDeductionAsync(NewDealDto newDealDto)
    {
        if (newDealDto == null)
        {
            _logger.LogError("Received null deal in ProfitDeductionAsync");
            return;
        }

        ulong positionId = newDealDto.PositionId;
        ulong outDealId = newDealDto.DealId;
        double outProfit = newDealDto.Profit;

        try
        {

            var inOrder = _mT5Operations.GetOrderByID(positionId, out var responseCode);

            if (responseCode != MTRetCode.MT_RET_OK)
            {
                _logger.LogCritical(
                     "Failed to get in order from MT5 for Data : {json} With Response code : {responseCode}.",
                     JsonSerializer.Serialize(newDealDto), responseCode);
                return;
            }

            if (inOrder == null)
            {
                _logger.LogWarning(
                    "No In  order found from MT5 with Data : {json} .",
                    JsonSerializer.Serialize(newDealDto));

                return;
            }

            DateTime inDealDateTime;
            try
            {
                var inDealTime = inOrder.TimeDone();
                inDealDateTime = DateTimeOffset.FromUnixTimeSeconds(inDealTime).DateTime;
                string Comment = $"Scalping Deduction #{outDealId}";
                await DeductProfitAsync(inDealDateTime, newDealDto.Time, newDealDto.Profit, newDealDto.Login, Comment, outDealId, newDealDto.Symbol, positionId, newDealDto.Volume, newDealDto.EntryType, newDealDto.ActionType);
            }
            catch (Exception timeEx)
            {
                _logger.LogError(
                    timeEx,
                    "Failed to process deal in ProfitDealAsync for PositionId={PositionId}, OutDealId={OutDealId}, Login={Login}",
                    positionId, outDealId, newDealDto.Login);

                inOrder?.Release();
                return;
            }

            inOrder?.Release();
        }
        catch (Exception ex)
        {
            try
            {
                var payload = JsonSerializer.Serialize(newDealDto);
                _logger.LogError(
                    ex,
                    "Unhandled error in ProfitDeductionAsync for deal payload: {Payload}",
                    payload);
            }
            catch (Exception logEx)
            {
                _logger.LogError(
                    logEx,
                    "Failed while logging error in ProfitDeductionAsync. DealId={OutDealId}, Login={Login} PositionId={PositionId}",
                    outDealId, newDealDto.Login, positionId);
            }
        }
    }

    private async Task DeductProfitAsync(
     DateTime inTime,
     DateTime outTime,
     double profit,
     ulong login,
     string comment,
     ulong outDealId,
     string symbol,
     ulong positionId,
     double volume,
     uint entry,
     uint action)
    {
        try
        {
            var diff = outTime - inTime;
            
            if (diff.TotalMinutes >= 3)
            {
                return;
            }

            if (profit <= 0)
            {
                _logger.LogInformation(
                    "Profit deduction skipped: OutDealId={OutDealId}, Login={Login}, Reason=Profit {Profit} <= 0",
                    outDealId, login, profit);
                return;
            }
            
            var profitDeal = new ProfitOutDeals
            {
                DealId = outDealId,
                Symbol = symbol,
                Login = login,
                ClosingTime = outTime,
                Entry = entry,
                Action = action,
                Volume = volume,
                PositionID = positionId,
                OpeningTime = inTime,
                ProfitOut = profit,
                Comment = comment
            };
            
            await _profitDeductionQueue.EnqueueAsync(profitDeal);

            _logger.LogInformation(
                "Enqueued ProfitDeduction DealId={DealId}, Login={Login}, Profit={Profit}, HoldingTime={HoldingMinutes} min",
                profitDeal.DealId, profitDeal.Login, profitDeal.ProfitOut, diff.TotalMinutes);
        }
        catch (OperationCanceledException)
        {
                        _logger.LogWarning(
                "Profit deduction enqueue canceled for DealId={DealId}, Login={Login}",
                outDealId, login);
        }
        catch (Exception ex)
        {
            
            _logger.LogError(
                ex,
                "Unexpected error in DeductProfitAsync. DealId={DealId}, Login={Login}, Profit={Profit}",
                outDealId, login, profit);
        }
    }


}

