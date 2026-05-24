// ============================================================
// File:    Backend/PlantProcess.Application/Audit/AuditLogContext.cs
// Task:    BE-ADD-001
// ============================================================

namespace PlantProcess.Application.Audit;

/// <summary>
/// All the context required to record one audit log entry.
/// Passed from the middleware to the service.
/// </summary>
public sealed record AuditLogContext(
    string HttpMethod,
    string Endpoint,
    string ActionCategory,
    string OutcomeStatus,
    string? UserId,
    string? UserName,
    string? ResourceType,
    string? ResourceId,
    string? ClientIp,
    string? UserAgent,
    string? CorrelationId,
    int? HttpStatusCode,
    string? MetadataJson);
