using System.Text.Json;
using TestScalpingBackend.Services;

public class DealProcessingService : BackgroundService
{
    private readonly IDealQueue _dealQueue;
    private readonly ScalpingDeduction _deductionService;
    private readonly ILogger<DealProcessingService> _logger;

    public DealProcessingService(
        IDealQueue dealQueue,
        ScalpingDeduction deductionService,
        ILogger<DealProcessingService> logger)
    {
        _dealQueue = dealQueue;
        _deductionService = deductionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Deal processing service started with {ThreadCount} consumers",
            Environment.ProcessorCount);

        var consumers = Enumerable.Range(0, Environment.ProcessorCount)
                                  .Select(i => Task.Run(() => ConsumeDeals(i, stoppingToken), stoppingToken));

        await Task.WhenAll(consumers);
    }

    private async Task ConsumeDeals(int consumerId, CancellationToken stoppingToken)
    {
        await foreach (var deal in _dealQueue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await _deductionService.ProfitDeductionAsync(deal);
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
                        "Consumer {ConsumerId} failed processing deal {DealId}. Payload: {Payload}",
                        consumerId, deal.DealId, json);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(
                        logEx,
                        "Consumer {ConsumerId} failed while logging error for deal {DealId}",
                        consumerId, deal?.DealId);
                }
            }
        }
    }

}

