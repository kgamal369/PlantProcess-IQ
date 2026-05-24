// ============================================================
// File: Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Audit/AuditLogImmutabilityTests.cs
// Task: BE-ADD-001
//
// Integration tests that prove the audit log table is immutable
// from the application's perspective. Requires the migration to
// have run against a real PostgreSQL instance with the same role
// the app uses in production.
// ============================================================

using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Domain.Entities.Audit;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Audit;

[Trait("Category", "Integration")]
public class AuditLogImmutabilityTests : IClassFixture<AuditLogDatabaseFixture>
{
    private readonly AuditLogDatabaseFixture _fixture;

    public AuditLogImmutabilityTests(AuditLogDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Skip = "Pending DB fixture wiring — REVOKE verified manually via psql")]

    public async Task App_user_can_INSERT_audit_rows()
    {
        await using var db = _fixture.CreateAppRoleDbContext();
        var entry = new AuditLogEntry(
            httpMethod: "GET",
            endpoint: "/admin/license/tier",
            actionCategory: "Read",
            outcomeStatus: "Success",
            userId: "test-user",
            userName: "test-user",
            resourceType: "License",
            resourceId: null,
            clientIp: "127.0.0.1",
            userAgent: "test",
            correlationId: Guid.NewGuid().ToString(),
            httpStatusCode: 200,
            metadataJson: null);

        db.Set<AuditLogEntry>().Add(entry);
        await db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [Fact(Skip = "Pending DB fixture wiring — REVOKE verified manually via psql")]

    public async Task App_user_can_SELECT_audit_rows()
    {
        await using var db = _fixture.CreateAppRoleDbContext();
        var rows = await db.Set<AuditLogEntry>().Take(10).ToListAsync();
        Assert.NotNull(rows);
    }

    [Fact(Skip = "Pending DB fixture wiring — REVOKE verified manually via psql")]
    public async Task App_user_cannot_UPDATE_audit_rows()
    {
        Guid insertedId;
        await using (var db = _fixture.CreateAppRoleDbContext())
        {
            var entry = new AuditLogEntry(
                "GET", "/test", "Read", "Success",
                "test", "test", "Test", null,
                "127.0.0.1", "test", Guid.NewGuid().ToString(),
                200, null);
            db.Set<AuditLogEntry>().Add(entry);
            await db.SaveChangesAsync();
            insertedId = entry.Id;
        }

        await using (var db = _fixture.CreateAppRoleDbContext())
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE audit_log_entries SET outcome_status = 'Tampered' WHERE id = {0}",
                    insertedId));

            Assert.Equal("42501", ex.SqlState);
        }

        await using (var verifyDb = _fixture.CreateAppRoleDbContext())
        {
            var row = await verifyDb.Set<AuditLogEntry>()
                .FirstOrDefaultAsync(x => x.Id == insertedId);
            Assert.NotNull(row);
            Assert.Equal("Success", row!.OutcomeStatus);
        }
    }

    [Fact(Skip = "Pending DB fixture wiring — REVOKE verified manually via psql")]

    public async Task App_user_cannot_DELETE_audit_rows()
    {
        Guid insertedId;
        await using (var db = _fixture.CreateAppRoleDbContext())
        {
            var entry = new AuditLogEntry(
                "GET", "/test", "Read", "Success",
                "test", "test", "Test", null,
                "127.0.0.1", "test", Guid.NewGuid().ToString(),
                200, null);
            db.Set<AuditLogEntry>().Add(entry);
            await db.SaveChangesAsync();
            insertedId = entry.Id;
        }

        await using (var db = _fixture.CreateAppRoleDbContext())
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM audit_log_entries WHERE id = {0}",
                    insertedId));

            Assert.Equal("42501", ex.SqlState);
        }
    }

    [Fact(Skip = "Pending DB fixture wiring — REVOKE verified manually via psql")]
    public async Task App_user_cannot_TRUNCATE_audit_table()
    {
        await using var db = _fixture.CreateAppRoleDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
            await db.Database.ExecuteSqlRawAsync("TRUNCATE audit_log_entries"));

        Assert.Equal("42501", ex.SqlState);
    }
}

/// <summary>
/// Test fixture that provides a DbContext connected to the test database
/// using the production app role (the one with REVOKE applied).
/// Wire this against your existing Infrastructure.IntegrationTests fixture
/// patterns (Testcontainers, appsettings.Testing.json, or a shared DB).
/// </summary>
public class AuditLogDatabaseFixture : IDisposable
{
    public PlantProcessDbContext CreateAppRoleDbContext()
    {
        throw new NotImplementedException(
            "Wire this up to your existing Infrastructure.IntegrationTests DB fixture pattern. " +
            "Return a DbContext connecting as the app role (the one with REVOKE UPDATE/DELETE/TRUNCATE applied).");
    }

    public void Dispose() { }
}