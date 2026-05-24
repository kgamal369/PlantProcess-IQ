// ============================================================
// File:    Backend/PlantProcess.Domain/Entities/Audit/AuditLogEntry.cs
// Task:    BE-ADD-001 (AuditLogEntry full implementation)
//
// Append-only domain entity representing one security-relevant action.
// Database-level privileges revoke UPDATE/DELETE on the table, so this
// entity has no mutator methods after construction.
// ============================================================

using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Audit;

/// <summary>
/// Append-only record of every security-relevant action.
/// Database privileges revoke UPDATE/DELETE on this table —
/// these entities can be created and read, never modified.
/// </summary>
public sealed class AuditLogEntry : BaseEntity
{
    // ─── Required ────────────────────────────────────────────
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>HTTP method (GET / POST / PUT / PATCH / DELETE).</summary>
    public string HttpMethod { get; private set; } = null!;

    /// <summary>Endpoint path (e.g. "/admin/connection-profiles").</summary>
    public string Endpoint { get; private set; } = null!;

    /// <summary>Read / Write / Delete / Admin.</summary>
    public string ActionCategory { get; private set; } = null!;

    /// <summary>Success / Failed / Forbidden / Unauthenticated.</summary>
    public string OutcomeStatus { get; private set; } = null!;

    // ─── Optional context ────────────────────────────────────
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }

    /// <summary>Resource type (e.g. "ConnectionProfile", "Dashboard").</summary>
    public string? ResourceType { get; private set; }

    /// <summary>Resource identifier (typically a GUID as string).</summary>
    public string? ResourceId { get; private set; }

    public string? ClientIp { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CorrelationId { get; private set; }

    public int? HttpStatusCode { get; private set; }

    /// <summary>Optional structured context (jsonb in PostgreSQL).</summary>
    public string? MetadataJson { get; private set; }

    // ─── EF Core ─────────────────────────────────────────────
    private AuditLogEntry() { }

    public AuditLogEntry(
        string httpMethod,
        string endpoint,
        string actionCategory,
        string outcomeStatus,
        string? userId,
        string? userName,
        string? resourceType,
        string? resourceId,
        string? clientIp,
        string? userAgent,
        string? correlationId,
        int? httpStatusCode,
        string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(httpMethod))
            throw new ArgumentException("HttpMethod is required.", nameof(httpMethod));
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint is required.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(actionCategory))
            throw new ArgumentException("ActionCategory is required.", nameof(actionCategory));
        if (string.IsNullOrWhiteSpace(outcomeStatus))
            throw new ArgumentException("OutcomeStatus is required.", nameof(outcomeStatus));

        OccurredAtUtc  = DateTime.UtcNow;
        HttpMethod     = httpMethod.ToUpperInvariant();
        Endpoint       = endpoint;
        ActionCategory = actionCategory;
        OutcomeStatus  = outcomeStatus;
        UserId         = userId;
        UserName       = userName;
        ResourceType   = resourceType;
        ResourceId     = resourceId;
        ClientIp       = clientIp;
        UserAgent      = userAgent;
        CorrelationId  = correlationId;
        HttpStatusCode = httpStatusCode;
        MetadataJson   = metadataJson;
    }
}
