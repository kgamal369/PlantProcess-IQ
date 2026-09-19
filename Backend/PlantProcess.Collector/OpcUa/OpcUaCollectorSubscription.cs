using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace PlantProcess.Collector.OpcUa;

/// <summary>Source-transport deadband mechanics, requested by the collector.</summary>
public enum OpcUaDeadbandKind
{
    None,
    Absolute,
    Percent
}

/// <summary>
/// What the collector asks the source for. These are transport mechanics of the source
/// subscription. They are not PPIQ recording semantics: a source notification is not a
/// durable business record, and an OPC deadband is not a recording deadband.
/// </summary>
public sealed record OpcUaSubscriptionRequest(
    int PublishingIntervalMs = 500,
    int SamplingIntervalMs = 250,
    uint QueueSize = 10,
    bool DiscardOldest = true,
    OpcUaDeadbandKind Deadband = OpcUaDeadbandKind.None,
    double DeadbandValue = 0,
    bool TriggerOnStatusAndValue = true)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PublishingIntervalMs < 50 || PublishingIntervalMs > 600000)
        {
            errors.Add("PublishingIntervalMs must be between 50 and 600000.");
        }

        if (SamplingIntervalMs < 0 || SamplingIntervalMs > 600000)
        {
            errors.Add("SamplingIntervalMs must be between 0 and 600000.");
        }

        if (QueueSize < 1 || QueueSize > 10000)
        {
            errors.Add("QueueSize must be between 1 and 10000.");
        }

        if (Deadband != OpcUaDeadbandKind.None && DeadbandValue <= 0)
        {
            errors.Add("A deadband kind other than None needs a positive DeadbandValue.");
        }

        return errors;
    }
}

/// <summary>Requested against server-revised transport facts. Neither overwrites the other.</summary>
public sealed record OpcUaMonitoredItemFact(
    OpcUaProviderFieldIdentity Identity,
    int RequestedSamplingIntervalMs,
    double? RevisedSamplingIntervalMs,
    uint RequestedQueueSize,
    uint? RevisedQueueSize,
    string RequestedFilter,
    string? EffectiveFilter,
    string MonitoringMode,
    string? SourceStatusCode,
    bool Created);

/// <summary>A sample the source actually delivered. Nothing here is generated locally.</summary>
public sealed record OpcUaSubscriptionSample(
    OpcUaProviderFieldIdentity Identity,
    string? Value,
    string SourceStatusCode,
    bool QualityIsGood,
    DateTimeOffset? SourceTimestampUtc,
    DateTimeOffset? ServerTimestampUtc,
    DateTimeOffset ReceivedAtUtc,
    long SequenceInSession);

/// <summary>
/// An interval during which the source connection was lost. It is evidence of possible
/// missed values. It is never filled in with fabricated or carried-forward samples;
/// downstream recording and reconciliation own what to do with it.
/// </summary>
public sealed record OpcUaSubscriptionGap(
    DateTimeOffset DetectedAtUtc,
    DateTimeOffset? RecoveredAtUtc,
    string Reason,
    long SamplesBeforeGap,
    bool Resubscribed);

/// <summary>The operation-level receipt of one subscription attempt.</summary>
public sealed record OpcUaSubscriptionReceipt(
    string SourceProfileId,
    string ConfigurationVersion,
    DateTimeOffset ObservedAtUtc,
    OpcUaCollectorOperationFact Operation,
    int RequestedPublishingIntervalMs,
    double? RevisedPublishingIntervalMs,
    IReadOnlyList<OpcUaMonitoredItemFact> Items,
    IReadOnlyList<OpcUaReadRefusal> Refusals);

/// <summary>
/// A live source subscription. It owns monitored items, the requested against revised
/// transport facts, the samples the source delivered and the gaps where it delivered
/// nothing. It owns no recording semantics, no durability and no business record.
/// </summary>
public sealed class OpcUaCollectorSubscriptionSession : IAsyncDisposable
{
    private readonly OpcUaCollectorSessionRuntime _runtime;
    private readonly ITelemetryContext _telemetry;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<OpcUaSubscriptionSample> _samples = new();
    private readonly ConcurrentDictionary<uint, OpcUaProviderFieldIdentity> _itemFields = new();
    private readonly List<OpcUaSubscriptionGap> _gaps = new();
    private readonly object _gate = new();

    private Subscription? _subscription;
    private OpcUaSubscriptionRequest _request = new();
    private IReadOnlyList<OpcUaProviderFieldIdentity> _fields = Array.Empty<OpcUaProviderFieldIdentity>();
    private long _sequence;
    private bool _disposed;

    public OpcUaCollectorSubscriptionSession(OpcUaCollectorSessionRuntime runtime, ITelemetryContext telemetry)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(telemetry);
        _runtime = runtime;
        _telemetry = telemetry;
        _logger = telemetry.LoggerFactory.CreateLogger("PlantProcess.Collector.OpcUa.Subscription");
    }

    /// <summary>Samples delivered by the source so far, oldest first.</summary>
    public IReadOnlyList<OpcUaSubscriptionSample> Samples => _samples.ToArray();

    /// <summary>Connection-loss windows observed while this subscription was active.</summary>
    public IReadOnlyList<OpcUaSubscriptionGap> Gaps
    {
        get
        {
            lock (_gate)
            {
                return _gaps.ToArray();
            }
        }
    }

    public OpcUaSubscriptionReceipt? LastReceipt { get; private set; }

    public Task<OpcUaSubscriptionReceipt> SubscribeAsync(
        IReadOnlyList<OpcUaProviderFieldIdentity> fields,
        OpcUaSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        IReadOnlyList<string> errors = request.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException("The subscription request is invalid: " + string.Join(" ", errors), nameof(request));
        }

        if (fields.Count == 0)
        {
            throw new ArgumentException("A subscription needs at least one field.", nameof(fields));
        }

        _fields = fields;
        _request = request;

        return _runtime.WithSessionAsync(
            (session, ct) => CreateAsync(session, fields, request, ct),
            () => Publish(new OpcUaSubscriptionReceipt(
                _runtime.SourceProfileId,
                _runtime.ConfigurationVersion,
                DateTimeOffset.UtcNow,
                new OpcUaCollectorOperationFact(
                    OpcUaCollectorOperations.Subscribe,
                    OpcUaCollectorOperationStatus.Refused,
                    "The collector session is not connected, so no subscription was created."),
                request.PublishingIntervalMs,
                null,
                Array.Empty<OpcUaMonitoredItemFact>(),
                fields
                    .Select(field => new OpcUaReadRefusal(
                        field,
                        OpcUaCollectorRefusalCodes.SessionNotConnected,
                        null,
                        "The collector session is not connected."))
                    .ToArray())),
            cancellationToken);
    }

    /// <summary>
    /// Records a connection loss and re-creates the subscription on the current session.
    /// Missed values are never reconstructed; the gap stays in the evidence.
    /// </summary>
    public async Task<OpcUaSubscriptionReceipt> ResubscribeAsync(string reason, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        OpcUaSubscriptionGap gap;
        lock (_gate)
        {
            gap = new OpcUaSubscriptionGap(
                DateTimeOffset.UtcNow,
                null,
                reason,
                Interlocked.Read(ref _sequence),
                false);
            _gaps.Add(gap);
        }

        await DetachAsync().ConfigureAwait(false);

        OpcUaSubscriptionReceipt receipt = await SubscribeAsync(_fields, _request, cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            int index = _gaps.IndexOf(gap);
            if (index >= 0)
            {
                _gaps[index] = gap with
                {
                    RecoveredAtUtc = DateTimeOffset.UtcNow,
                    Resubscribed = receipt.Operation.Status == OpcUaCollectorOperationStatus.Executed
                };
            }
        }

        return receipt;
    }

    private async Task<OpcUaSubscriptionReceipt> CreateAsync(
        ISession session,
        IReadOnlyList<OpcUaProviderFieldIdentity> fields,
        OpcUaSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var refusals = new List<OpcUaReadRefusal>();
        var subscription = new Subscription(_telemetry)
        {
            DisplayName = "ppiq-collector:" + _runtime.SourceProfileId,
            PublishingInterval = request.PublishingIntervalMs,
            KeepAliveCount = 10,
            LifetimeCount = 1000,
            MaxNotificationsPerPublish = 1000,
            PublishingEnabled = true,
            TimestampsToReturn = TimestampsToReturn.Both
        };

        if (!session.AddSubscription(subscription))
        {
            throw new InvalidOperationException("The session refused the subscription.");
        }

        await subscription.CreateAsync(cancellationToken).ConfigureAwait(false);

        var created = new List<(OpcUaProviderFieldIdentity Field, MonitoredItem Item)>();

        foreach (OpcUaProviderFieldIdentity field in fields)
        {
            if (!field.AttributeIsSupported)
            {
                refusals.Add(new OpcUaReadRefusal(
                    field,
                    OpcUaCollectorRefusalCodes.AttributeNotSupported,
                    null,
                    "This build subscribes to the Value attribute only."));
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

            var item = new MonitoredItem(_telemetry)
            {
                DisplayName = field.Canonical(),
                StartNodeId = nodeId,
                AttributeId = field.AttributeId,
                IndexRange = field.IndexRange,
                MonitoringMode = MonitoringMode.Reporting,
                SamplingInterval = request.SamplingIntervalMs,
                QueueSize = request.QueueSize,
                DiscardOldest = request.DiscardOldest,
                Filter = BuildFilter(request)
            };

            item.Notification += OnNotification;
            subscription.AddItem(item);
            created.Add((field, item));
            _itemFields[item.ClientHandle] = field;
        }

        await subscription.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);

        _subscription = subscription;

        var facts = new List<OpcUaMonitoredItemFact>();
        foreach ((OpcUaProviderFieldIdentity field, MonitoredItem item) in created)
        {
            bool itemCreated = item.Created && ServiceResult.IsGood(item.Status?.Error);

            facts.Add(new OpcUaMonitoredItemFact(
                field,
                request.SamplingIntervalMs,
                itemCreated ? item.Status!.SamplingInterval : null,
                request.QueueSize,
                itemCreated ? item.Status!.QueueSize : null,
                DescribeFilter(request),
                itemCreated ? DescribeFilter(item.Status!.Filter) : null,
                item.Status?.MonitoringMode.ToString() ?? MonitoringMode.Disabled.ToString(),
                item.Status?.Error is null ? null : OpcUaCollectorStatusNames.Describe(item.Status.Error.StatusCode.Code),
                itemCreated));

            if (!itemCreated)
            {
                refusals.Add(new OpcUaReadRefusal(
                    field,
                    OpcUaCollectorRefusalCodes.SubscriptionRefused,
                    item.Status?.Error is null ? null : OpcUaCollectorStatusNames.Describe(item.Status.Error.StatusCode.Code),
                    "The source refused this monitored item."));
            }
        }

        _logger.LogInformation(
            "Subscription created with {ItemCount} monitored items and {RefusalCount} refusals.",
            facts.Count,
            refusals.Count);

        OpcUaCollectorOperationStatus status = facts.Any(fact => fact.Created)
            ? OpcUaCollectorOperationStatus.Executed
            : OpcUaCollectorOperationStatus.Refused;

        return Publish(new OpcUaSubscriptionReceipt(
            _runtime.SourceProfileId,
            _runtime.ConfigurationVersion,
            DateTimeOffset.UtcNow,
            new OpcUaCollectorOperationFact(
                OpcUaCollectorOperations.Subscribe,
                status,
                status == OpcUaCollectorOperationStatus.Executed
                    ? "Monitored items created. Requested and server-revised transport values are recorded separately."
                    : "No monitored item was created; every field carries a typed refusal."),
            request.PublishingIntervalMs,
            subscription.CurrentPublishingInterval,
            facts,
            refusals));
    }

    private static MonitoringFilter? BuildFilter(OpcUaSubscriptionRequest request)
    {
        if (request.Deadband == OpcUaDeadbandKind.None && !request.TriggerOnStatusAndValue)
        {
            return null;
        }

        return new DataChangeFilter
        {
            Trigger = request.TriggerOnStatusAndValue ? DataChangeTrigger.StatusValue : DataChangeTrigger.Status,
            DeadbandType = (uint)(request.Deadband switch
            {
                OpcUaDeadbandKind.Absolute => DeadbandType.Absolute,
                OpcUaDeadbandKind.Percent => DeadbandType.Percent,
                _ => DeadbandType.None
            }),
            DeadbandValue = request.DeadbandValue
        };
    }

    private static string DescribeFilter(OpcUaSubscriptionRequest request)
        => "trigger=" + (request.TriggerOnStatusAndValue ? DataChangeTrigger.StatusValue : DataChangeTrigger.Status) +
           ";deadband=" + request.Deadband +
           ";deadbandValue=" + request.DeadbandValue;

    private static string DescribeFilter(MonitoringFilter? filter)
    {
        if (filter is not DataChangeFilter dataChange)
        {
            return "none";
        }

        OpcUaDeadbandKind kind = (DeadbandType)dataChange.DeadbandType switch
        {
            DeadbandType.Absolute => OpcUaDeadbandKind.Absolute,
            DeadbandType.Percent => OpcUaDeadbandKind.Percent,
            _ => OpcUaDeadbandKind.None
        };

        return "trigger=" + dataChange.Trigger + ";deadband=" + kind + ";deadbandValue=" + dataChange.DeadbandValue;
    }

    private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs eventArgs)
    {
        if (eventArgs.NotificationValue is not MonitoredItemNotification notification)
        {
            return;
        }

        if (!_itemFields.TryGetValue(item.ClientHandle, out OpcUaProviderFieldIdentity? field))
        {
            return;
        }

        DataValue value = notification.Value;
        long sequence = Interlocked.Increment(ref _sequence);

        _samples.Enqueue(new OpcUaSubscriptionSample(
            field,
            value.Value?.ToString(),
            OpcUaCollectorStatusNames.Describe(value.StatusCode.Code),
            StatusCode.IsGood(value.StatusCode),
            OpcUaCollectorBoundedReadService.ToUtc(value.SourceTimestamp),
            OpcUaCollectorBoundedReadService.ToUtc(value.ServerTimestamp),
            DateTimeOffset.UtcNow,
            sequence));
    }

    private OpcUaSubscriptionReceipt Publish(OpcUaSubscriptionReceipt receipt)
    {
        LastReceipt = receipt;
        return receipt;
    }

    private async Task DetachAsync()
    {
        Subscription? subscription = _subscription;
        _subscription = null;
        _itemFields.Clear();

        if (subscription is null)
        {
            return;
        }

        foreach (MonitoredItem item in subscription.MonitoredItems)
        {
            item.Notification -= OnNotification;
        }

        try
        {
            await subscription.DeleteAsync(true).ConfigureAwait(false);
        }
        catch (Exception deleteError) when (deleteError is not OutOfMemoryException)
        {
            _logger.LogWarning("The source subscription could not be deleted cleanly: {Reason}.", deleteError.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await DetachAsync().ConfigureAwait(false);
        _disposed = true;
    }
}