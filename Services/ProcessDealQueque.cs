using System.Collections.Concurrent;
using System.Threading.Channels;
using TestScalpingBackend.Models;

public interface IProfitDeductionQueue
{
    ValueTask EnqueueAsync(ProfitOutDeals deal, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ProfitOutDeals> DequeueAllAsync(CancellationToken cancellationToken);
}

public class ProfitDeductionQueue : IProfitDeductionQueue
{   
    
    private readonly Channel<ProfitOutDeals> _channel;
    private readonly ConcurrentDictionary<ulong, byte> _inQueue; 

    public ProfitDeductionQueue()
    {
        _channel = Channel.CreateUnbounded<ProfitOutDeals>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        _inQueue = new ConcurrentDictionary<ulong, byte>();
    }

    public async ValueTask EnqueueAsync(ProfitOutDeals deal, CancellationToken cancellationToken = default)
    {
        if (_inQueue.TryAdd(deal.DealId, 0))
        {
            await _channel.Writer.WriteAsync(deal, cancellationToken);
        }
    }

    public async IAsyncEnumerable<ProfitOutDeals> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var deal in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            _inQueue.TryRemove(deal.DealId, out _);
            yield return deal;
        }
    }
}
