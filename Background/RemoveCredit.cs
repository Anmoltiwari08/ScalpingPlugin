using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TestScalpingBackend.Models;
using System.Text.Json;
using TestScalpingBackend.Services;
using MetaQuotes.MT5CommonAPI;

public class ProfitDeductionService : BackgroundService
{
    private readonly IProfitDeductionQueue _queue;
    private readonly IHubContext<ProfitOutDealHub> _hubContext;
    private readonly AppDbContext _context;
    private readonly ILogger<ProfitDeductionService> _logger;
    private MT5Operations mT5Operations;

    public ProfitDeductionService(
        IProfitDeductionQueue queue,
        IHubContext<ProfitOutDealHub> hubContext,
        AppDbContext context,
        ILogger<ProfitDeductionService> logger,
        MT5Operations mT5Operations
        )
    {
        _queue = queue;
        _hubContext = hubContext;
        _context = context;
        _logger = logger;
        this.mT5Operations = mT5Operations;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProfitDeductionService started with {ThreadCount} consumers", Environment.ProcessorCount);

        var consumers = Enumerable.Range(0, Environment.ProcessorCount)
                                  .Select(i => Task.Run(() => Consume(i, stoppingToken), stoppingToken));

        await Task.WhenAll(consumers);
    }

    private async Task Consume(int consumerId, CancellationToken stoppingToken)
    {   
        await foreach (var deal in _queue.DequeueAllAsync(stoppingToken))
        {         
            try  
            {
                 
                bool exists = await _context.ProfitOutDeals
                    .AnyAsync(d => d.DealId == deal.DealId, stoppingToken);

                if (exists)
                {
                    _logger.LogWarning(
                        "Consumer {ConsumerId}: DealId={DealId}, Login={Login} already exists. Skipping.",
                        consumerId, deal.DealId, deal.Login);
                    continue;
                }

                var updateResult = mT5Operations.UpdateCredit(
                    deal.Login, -deal.ProfitOut, 2, deal.Comment, out ulong mtDealId);

                if (updateResult != MTRetCode.MT_RET_REQUEST_DONE)
                {
                    _logger.LogError(
                        "Consumer {ConsumerId}: Failed to update credit for DealId={DealId}, Login={Login}. ErrorCode={ErrorCode}",
                        consumerId, deal.DealId, deal.Login, updateResult);
                    continue;
                }

                _context.ProfitOutDeals.Add(deal);
                await _context.SaveChangesAsync(stoppingToken);

                await _hubContext.Clients.All.SendAsync("ReceiveProfitOutDeals", deal, stoppingToken);

                _logger.LogInformation(
                    "Consumer {ConsumerId}: Successfully processed DealId={DealId}, Login={Login}",
                    consumerId, deal.DealId, deal.Login);

            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Consumer {ConsumerId} canceled gracefully", consumerId);
                return;
            }
            catch (Exception ex)
            {
                try
                {
                    var json = JsonSerializer.Serialize(deal);
                    _logger.LogError(
                        ex,
                        "Consumer {ConsumerId}: Error processing ProfitDeduction for Deal data={Json}",
                        consumerId, json);
                }
                catch
                {
                    _logger.LogError(ex, "Consumer {ConsumerId}: Error processing deal (failed to serialize)", consumerId);
                }
            }
        }
    }

}

