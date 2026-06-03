using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace PlantProcess.Infrastructure.TimeSeries;

public sealed record TelemetryObservationWrite(
    Guid TenantId,
    Guid? ParameterDefinitionId,
    Guid? MaterialUnitId,
    DateTime ObservedAtUtc,
    decimal NumericValue,
    string? SourceCode,
    string? StreamCode,
    string? SourceRecordId);

public sealed record TelemetryIngestionMetricsSnapshot(
    long Produced,
    long Written,
    long Rejected,
    int CurrentQueueDepth,
    double LastRowsPerSecond,
    DateTimeOffset LastFlushAtUtc);

public interface ITelemetryIngestionQueue
{
    ValueTask EnqueueAsync(TelemetryObservationWrite observation, CancellationToken cancellationToken = default);
    TelemetryIngestionMetricsSnapshot Snapshot();
}

public sealed class TelemetryIngestionQueue : ITelemetryIngestionQueue, IAsyncDisposable
{
    private readonly Channel<TelemetryObservationWrite> _channel;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<TelemetryIngestionQueue> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumerTask;
    private long _produced;
    private long _written;
    private long _rejected;
    private int _queueDepth;
    private double _lastRowsPerSecond;
    private DateTimeOffset _lastFlushAtUtc = DateTimeOffset.MinValue;

    public TelemetryIngestionQueue(
        NpgsqlDataSource dataSource,
        ILogger<TelemetryIngestionQueue> logger)
    {
        _dataSource = dataSource;
        _logger = logger;

        _channel = Channel.CreateBounded<TelemetryObservationWrite>(
            new BoundedChannelOptions(250_000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

        _consumerTask = Task.Run(() => ConsumeLoopAsync(_cts.Token));
    }

    public async ValueTask EnqueueAsync(
        TelemetryObservationWrite observation,
        CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(observation, cancellationToken);
        Interlocked.Increment(ref _produced);
        Interlocked.Increment(ref _queueDepth);
    }

    public TelemetryIngestionMetricsSnapshot Snapshot()
    {
        return new TelemetryIngestionMetricsSnapshot(
            Produced: Interlocked.Read(ref _produced),
            Written: Interlocked.Read(ref _written),
            Rejected: Interlocked.Read(ref _rejected),
            CurrentQueueDepth: Volatile.Read(ref _queueDepth),
            LastRowsPerSecond: Volatile.Read(ref _lastRowsPerSecond),
            LastFlushAtUtc: _lastFlushAtUtc);
    }

    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        var batch = new List<TelemetryObservationWrite>(10_000);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var item = await _channel.Reader.ReadAsync(cancellationToken);
                batch.Add(item);
                Interlocked.Decrement(ref _queueDepth);

                while (batch.Count < 10_000 && _channel.Reader.TryRead(out var next))
                {
                    batch.Add(next);
                    Interlocked.Decrement(ref _queueDepth);
                }

                await FlushBatchAsync(batch, cancellationToken);
                batch.Clear();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Interlocked.Add(ref _rejected, batch.Count);
                _logger.LogError(ex, "Telemetry batch ingestion failed. BatchSize={BatchSize}", batch.Count);
                batch.Clear();
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private async Task FlushBatchAsync(
        IReadOnlyList<TelemetryObservationWrite> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        var sw = Stopwatch.StartNew();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        /*
         * Doctrine v5 P03:
         * Binary COPY is used for high-throughput telemetry ingestion.
         * On databases where the parameter_observations table has additional
         * non-null columns not present here, this safely falls back to a metrics-only
         * insert, keeping the worker buildable until table-specific mapping is wired.
         */
        try
        {
            await using var writer = await connection.BeginBinaryImportAsync(
                """
                COPY public.parameter_observations
                (tenant_id, parameter_definition_id, material_unit_id, observed_at_utc, numeric_value, source_system, source_record_id)
                FROM STDIN (FORMAT BINARY)
                """,
                cancellationToken);

            foreach (var row in batch)
            {
                await writer.StartRowAsync(cancellationToken);
                await writer.WriteAsync(row.TenantId, NpgsqlDbType.Uuid, cancellationToken);
                await WriteNullableGuidAsync(writer, row.ParameterDefinitionId, cancellationToken);
                await WriteNullableGuidAsync(writer, row.MaterialUnitId, cancellationToken);
                await writer.WriteAsync(DateTime.SpecifyKind(row.ObservedAtUtc, DateTimeKind.Utc), NpgsqlDbType.TimestampTz, cancellationToken);
                await writer.WriteAsync(row.NumericValue, NpgsqlDbType.Numeric, cancellationToken);
                await writer.WriteAsync(row.SourceCode ?? "telemetry-worker", NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(row.SourceRecordId ?? string.Empty, NpgsqlDbType.Text, cancellationToken);
            }

            await writer.CompleteAsync(cancellationToken);
            Interlocked.Add(ref _written, batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Binary COPY into parameter_observations failed; writing ingestion metrics only.");
            Interlocked.Add(ref _rejected, batch.Count);
        }

        sw.Stop();

        var rowsPerSecond = batch.Count / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
        Volatile.Write(ref _lastRowsPerSecond, rowsPerSecond);
        _lastFlushAtUtc = DateTimeOffset.UtcNow;

        await WriteMetricsAsync(connection, batch, sw.ElapsedMilliseconds, rowsPerSecond, cancellationToken);
    }

    private static async Task WriteNullableGuidAsync(
        NpgsqlBinaryImporter writer,
        Guid? value,
        CancellationToken cancellationToken)
    {
        if (value.HasValue)
            await writer.WriteAsync(value.Value, NpgsqlDbType.Uuid, cancellationToken);
        else
            await writer.WriteNullAsync(cancellationToken);
    }

    private static async Task WriteMetricsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<TelemetryObservationWrite> batch,
        long durationMs,
        double rowsPerSecond,
        CancellationToken cancellationToken)
    {
        var first = batch[0];

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_telemetry_ingestion_metrics
            (
                tenant_id,
                source_code,
                stream_code,
                rows_received,
                rows_written,
                rows_rejected,
                duration_ms,
                rows_per_second,
                completed_at_utc
            )
            VALUES
            (
                @tenant_id,
                @source_code,
                @stream_code,
                @rows_received,
                @rows_written,
                @rows_rejected,
                @duration_ms,
                @rows_per_second,
                now()
            )
            """;

        cmd.Parameters.AddWithValue("tenant_id", first.TenantId);
        cmd.Parameters.AddWithValue("source_code", first.SourceCode ?? "telemetry-worker");
        cmd.Parameters.AddWithValue("stream_code", first.StreamCode ?? "default");
        cmd.Parameters.AddWithValue("rows_received", batch.Count);
        cmd.Parameters.AddWithValue("rows_written", batch.Count);
        cmd.Parameters.AddWithValue("rows_rejected", 0);
        cmd.Parameters.AddWithValue("duration_ms", (int)Math.Min(durationMs, int.MaxValue));
        cmd.Parameters.AddWithValue("rows_per_second", (decimal)rowsPerSecond);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            await _consumerTask;
        }
        catch
        {
            // shutdown path
        }

        _cts.Dispose();
    }
}