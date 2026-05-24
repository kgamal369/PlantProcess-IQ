// ============================================================
// File:    Backend/PlantProcess.Application/Audit/IAuditLogService.cs
// Task:    BE-ADD-001
// ============================================================

namespace PlantProcess.Application.Audit;

public interface IAuditLogService
{
    /// <summary>
    /// Persist one audit log entry. Best-effort — failure to write
    /// must NEVER propagate to the caller. The middleware swallows
    /// any exception from this method.
    /// </summary>
    Task RecordAsync(AuditLogContext context, CancellationToken cancellationToken);
}
