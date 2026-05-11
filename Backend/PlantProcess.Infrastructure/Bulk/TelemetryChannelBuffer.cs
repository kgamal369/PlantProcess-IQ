using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PlantProcess.Infrastructure.Bulk;

public interface ITelemetryChannelBuffer
{
    ValueTask ProduceAsync(ParameterObservationInsertRow observation, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ParameterObservationInsertRow> ConsumeAllAsync(CancellationToken cancellationToken = default);
}

public class TelemetryChannelBuffer : ITelemetryChannelBuffer
{
    private readonly Channel<ParameterObservationInsertRow> _channel;
    private readonly ILogger<TelemetryChannelBuffer> _logger;

    public TelemetryChannelBuffer(ILogger<TelemetryChannelBuffer> logger)
    {
        _logger = logger;

        // Bounded channel prevents OutOfMemoryExceptions. 
        // If the DB crashes, it holds up to 100,000 telemetry points in RAM.
        var options = new BoundedChannelOptions(100_000)
        {
            // If the buffer is full, API threads will wait until space frees up (Backpressure)
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // Only one background worker reads from this
            SingleWriter = false // Many API threads can write to this simultaneously
        };

        _channel = Channel.CreateBounded<ParameterObservationInsertRow>(options);
    }

    public async ValueTask ProduceAsync(ParameterObservationInsertRow observation, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(observation, cancellationToken);
    }

    public IAsyncEnumerable<ParameterObservationInsertRow> ConsumeAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}