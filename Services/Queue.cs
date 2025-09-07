using System.Threading.Channels;
using TestScalpingBackend.Models;

public interface IDealQueue
{
    ValueTask EnqueueAsync(NewDealDto deal, CancellationToken cancellationToken = default);
    IAsyncEnumerable<NewDealDto> DequeueAllAsync(CancellationToken cancellationToken);
}

public class DealQueue : IDealQueue
{
    private readonly Channel<NewDealDto> _channel;

    public DealQueue()
    {
        _channel = Channel.CreateUnbounded<NewDealDto>(new UnboundedChannelOptions
        {
            SingleReader = false, // we want multiple consumers
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(NewDealDto deal, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(deal, cancellationToken);

    public IAsyncEnumerable<NewDealDto> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

}

