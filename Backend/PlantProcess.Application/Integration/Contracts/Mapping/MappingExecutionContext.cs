namespace PlantProcess.Application.Integration.Contracts.Mapping;

/// <summary>
/// PPIQ T-099. The identity and the duplicate memory of one execution.
///
/// TENANT IS RESOLVED ONCE, BEFORE THE FIRST ROW. Quarantine evidence carries a
/// tenant, and a tenant that cannot be resolved uniquely is an admission
/// failure rather than a row refusal: refusing a thousand rows individually for
/// a fault none of them caused would be a false taxonomy.
///
/// The duplicate tracker is scoped to this execution on purpose. PV05 is a
/// collision between two staged rows in the run in front of us; a collision with
/// persisted canonical truth is PV04 and is a different question.
/// </summary>
public sealed class MappingExecutionContext
{
    private readonly Dictionary<string, int> _businessKeys = new(StringComparer.OrdinalIgnoreCase);

    public MappingExecutionContext(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant identity is required for a mapping execution.", nameof(tenantId));

        TenantId = tenantId;
    }

    public Guid TenantId { get; }

    /// <summary>
    /// Records a governed business key for this execution. Returns the row
    /// number that already claimed it, or null when this row is the first.
    /// </summary>
    public int? ClaimBusinessKey(string businessKey, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(businessKey))
            return null;

        if (_businessKeys.TryGetValue(businessKey, out var owner))
            return owner;

        _businessKeys[businessKey] = rowNumber;
        return null;
    }
}
