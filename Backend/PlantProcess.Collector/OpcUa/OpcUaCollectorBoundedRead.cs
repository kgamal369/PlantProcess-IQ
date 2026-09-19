using System.Globalization;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace PlantProcess.Collector.OpcUa;

/// <summary>One observation exactly as the source returned it.</summary>
public sealed record OpcUaObservation(
    OpcUaProviderFieldIdentity Identity,
    string? Value,
    string ValueTypeName,
    string SourceStatusCode,
    bool QualityIsGood,
    DateTimeOffset? SourceTimestampUtc,
    DateTimeOffset? ServerTimestampUtc);

/// <summary>A field the read could not return. It is never replaced by a substitute value.</summary>
public sealed record OpcUaReadRefusal(
    OpcUaProviderFieldIdentity Identity,
    string RefusalCode,
    string? SourceStatusCode,
    string Detail);

/// <summary>
/// The operation-level receipt of one bounded read.
///
/// RequestedScope and AchievedScope are separate facts. An ordinary multi-node OPC read
/// is a set of independent reads and is never a controller-atomic snapshot, which is why
/// AtomicSourceSnapshot is always false in this build.
/// </summary>
public sealed record OpcUaBoundedReadReceipt(
    string SourceProfileId,
    string ConfigurationVersion,
    DateTimeOffset ObservedAtUtc,
    OpcUaCollectorOperationFact Operation,
    int RequestedScope,
    int AchievedScope,
    bool AtomicSourceSnapshot,
    string ScopeEvidence,
    IReadOnlyList<OpcUaObservation> Observations,
    IReadOnlyList<OpcUaReadRefusal> Refusals);

/// <summary>
/// Bounded read of source values. It is an operation of its own: a green browse or a green
/// subscription never proves it, and it proves neither of them.
/// </summary>
public sealed class OpcUaCollectorBoundedReadService
{
    /// <summary>The maximum number of fields one bounded read may request.</summary>
    public const int MaxFieldsPerRead = 500;

    private readonly OpcUaCollectorSessionRuntime _runtime;
    private readonly ILogger _logger;

    public OpcUaCollectorBoundedReadService(OpcUaCollectorSessionRuntime runtime, ITelemetryContext telemetry)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(telemetry);
        _runtime = runtime;
        _logger = telemetry.LoggerFactory.CreateLogger("PlantProcess.Collector.OpcUa.BoundedRead");
    }

    public Task<OpcUaBoundedReadReceipt> ReadAsync(
        IReadOnlyList<OpcUaProviderFieldIdentity> fields,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Count == 0 || fields.Count > MaxFieldsPerRead)
        {
            throw new ArgumentException(
                "A bounded read requests between 1 and " + MaxFieldsPerRead + " fields.",
                nameof(fields));
        }

        return _runtime.WithSessionAsync(
            (session, ct) => ExecuteAsync(session, fields, ct),
            () => new OpcUaBoundedReadReceipt(
                _runtime.SourceProfileId,
                _runtime.ConfigurationVersion,
                DateTimeOffset.UtcNow,
                new OpcUaCollectorOperationFact(
                    OpcUaCollectorOperations.BoundedRead,
                    OpcUaCollectorOperationStatus.Refused,
                    "The collector session is not connected, so nothing was read."),
                fields.Count,
                0,
                false,
                "No read was attempted.",
                Array.Empty<OpcUaObservation>(),
                fields
                    .Select(field => new OpcUaReadRefusal(
                        field,
                        OpcUaCollectorRefusalCodes.SessionNotConnected,
                        null,
                        "The collector session is not connected."))
                    .ToArray()),
            cancellationToken);
    }

    private async Task<OpcUaBoundedReadReceipt> ExecuteAsync(
        ISession session,
        IReadOnlyList<OpcUaProviderFieldIdentity> fields,
        CancellationToken cancellationToken)
    {
        var refusals = new List<OpcUaReadRefusal>();
        var resolved = new List<(OpcUaProviderFieldIdentity Field, ReadValueId Request)>();

        foreach (OpcUaProviderFieldIdentity field in fields)
        {
            if (!field.AttributeIsSupported)
            {
                refusals.Add(new OpcUaReadRefusal(
                    field,
                    OpcUaCollectorRefusalCodes.AttributeNotSupported,
                    null,
                    "This build acquires values from the Value attribute only."));
                continue;
            }

            NodeId? nodeId = field.TryResolve(session.MessageContext.NamespaceUris);
            if (nodeId is null)
            {
                refusals.Add(new OpcUaReadRefusal(
                    field,
                    OpcUaCollectorRefusalCodes.NamespaceNotPublished,
                    null,
                    "The server does not publish the namespace URI of this field."));
                continue;
            }

            resolved.Add((field, new ReadValueId
            {
                NodeId = nodeId,
                AttributeId = field.AttributeId,
                IndexRange = field.IndexRange
            }));
        }

        var observations = new List<OpcUaObservation>();

        if (resolved.Count > 0)
        {
            var requests = new ReadValueIdCollection(resolved.Select(entry => entry.Request));

            ReadResponse response;
            try
            {
                response = await session
                    .ReadAsync(null, 0, TimestampsToReturn.Both, requests, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ServiceResultException readError)
            {
                foreach ((OpcUaProviderFieldIdentity field, ReadValueId _) in resolved)
                {
                    refusals.Add(new OpcUaReadRefusal(
                        field,
                        OpcUaCollectorRefusalCodes.ReadRefused,
                        OpcUaCollectorStatusNames.Describe(readError.StatusCode),
                        readError.Message));
                }

                return Build(fields.Count, observations, refusals, OpcUaCollectorOperationStatus.Refused);
            }

            for (int index = 0; index < resolved.Count; index++)
            {
                OpcUaProviderFieldIdentity field = resolved[index].Field;
                DataValue value = response.Results[index];

                if (StatusCode.IsBad(value.StatusCode))
                {
                    refusals.Add(new OpcUaReadRefusal(
                        field,
                        OpcUaCollectorRefusalCodes.NodeNotAccessible,
                        OpcUaCollectorStatusNames.Describe(value.StatusCode.Code),
                        "The source refused this field. No value was substituted."));
                    continue;
                }

                observations.Add(Describe(field, value));
            }
        }

        _logger.LogInformation(
            "Bounded read requested {RequestedScope} fields and achieved {AchievedScope}.",
            fields.Count,
            observations.Count);

        OpcUaCollectorOperationStatus status = observations.Count > 0
            ? OpcUaCollectorOperationStatus.Executed
            : OpcUaCollectorOperationStatus.Refused;

        return Build(fields.Count, observations, refusals, status);
    }

    internal static OpcUaObservation Describe(OpcUaProviderFieldIdentity field, DataValue value)
    {
        object? raw = value.Value;
        string? rendered = raw switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => raw.ToString()
        };

        return new OpcUaObservation(
            field,
            rendered,
            raw?.GetType().Name ?? "null",
            OpcUaCollectorStatusNames.Describe(value.StatusCode.Code),
            StatusCode.IsGood(value.StatusCode),
            ToUtc(value.SourceTimestamp),
            ToUtc(value.ServerTimestamp));
    }

    internal static DateTimeOffset? ToUtc(DateTime timestamp)
        => timestamp == DateTime.MinValue
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));

    private OpcUaBoundedReadReceipt Build(
        int requestedScope,
        IReadOnlyList<OpcUaObservation> observations,
        IReadOnlyList<OpcUaReadRefusal> refusals,
        OpcUaCollectorOperationStatus status)
    {
        return new OpcUaBoundedReadReceipt(
            _runtime.SourceProfileId,
            _runtime.ConfigurationVersion,
            DateTimeOffset.UtcNow,
            new OpcUaCollectorOperationFact(
                OpcUaCollectorOperations.BoundedRead,
                status,
                status == OpcUaCollectorOperationStatus.Executed
                    ? "Bounded read executed. Each field carries its own status code and timestamps."
                    : "Bounded read returned no observation. Every requested field carries a typed refusal."),
            requestedScope,
            observations.Count,
            false,
            "Independent per-node reads. This is not a controller-atomic snapshot and is never recorded as one.",
            observations,
            refusals);
    }
}