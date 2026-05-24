// ============================================================
// File: Backend/PlantProcess.Infrastructure/Audit/AuditLogService.cs
// Task: BE-ADD-001 (revised — uses IServiceScopeFactory)
//
// Why IServiceScopeFactory instead of IDbContextFactory:
//   IDbContextFactory requires AddDbContextFactory<T>() to be registered,
//   which this codebase doesn't currently do. IServiceScopeFactory is a
//   built-in framework service, always available, and gives equivalent
//   isolation — each audit write runs in its own scope, separate from
//   the calling request's scope. This is the same pattern used by the
//   auto-migrate block in Program.cs.
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Audit;
using PlantProcess.Domain.Entities.Audit;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Audit;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RecordAsync(AuditLogContext c, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PlantProcessDbContext>();

            var entry = new AuditLogEntry(
                httpMethod:     c.HttpMethod,
                endpoint:       c.Endpoint,
                actionCategory: c.ActionCategory,
                outcomeStatus:  c.OutcomeStatus,
                userId:         c.UserId,
                userName:       c.UserName,
                resourceType:   c.ResourceType,
                resourceId:     c.ResourceId,
                clientIp:       c.ClientIp,
                userAgent:      c.UserAgent,
                correlationId:  c.CorrelationId,
                httpStatusCode: c.HttpStatusCode,
                metadataJson:   c.MetadataJson);

            db.Set<AuditLogEntry>().Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // The audit log must never break the application — log and continue.
            // Request succeeded; we just lost an audit row.
            _logger.LogWarning(ex,
                "AuditLogService: failed to persist audit entry for {Method} {Endpoint}",
                c.HttpMethod, c.Endpoint);
        }
    }
}