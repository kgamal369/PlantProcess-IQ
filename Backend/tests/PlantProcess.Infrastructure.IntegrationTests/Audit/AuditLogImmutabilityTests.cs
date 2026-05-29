// ============================================================
// File: Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Audit/AuditLogImmutabilityTests.cs
// Task: BE-ADD-001
//
// Purpose:
//   Local runnable proof that audit_log_entries is append-only at
//   database level using trigger guards.
//
// Why trigger-based:
//   The current local DB user does not have CREATEROLE, so we cannot
//   create a separate plantprocess_app runtime role in this environment.
//   This test proves UPDATE / DELETE / TRUNCATE are blocked by the DB.
//
// How these tests behave:
//   These are OPT-IN integration tests. They require a live PostgreSQL
//   that already has the audit_log_entries table (from EF migrations)
//   and the append-only triggers (from script 096_harden_audit_log_immutability.sql).
//
//   When the environment variable below is NOT set, every test in this
//   class is SKIPPED (not failed), so `dotnet test` stays green in
//   environments without a configured database (e.g. CI, a fresh laptop).
//
//   To actually run them, set the connection string and apply script 096:
//     PPIQ_AUDIT_TRIGGER_TEST_CONNECTION
//   Example value:
//     Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=YOUR_PASSWORD
//
// Expected SQLSTATE when a mutation is correctly blocked:
//   P0001 = blocked by prevent_audit_log_mutation trigger.
//   42501 = blocked by privilege revocation, if later you add role isolation.
// ============================================================

using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Domain.Entities.Audit;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Audit;

[Trait("Category", "Integration")]
[Trait("Task", "BE-ADD-001")]
public sealed class AuditLogImmutabilityTests : IClassFixture<AuditLogDatabaseFixture>
{
    private const string SkipReason =
        "Opt-in integration test. Set PPIQ_AUDIT_TRIGGER_TEST_CONNECTION to a PostgreSQL " +
        "connection string (with EF migrations applied and script 096 run) to execute this " +
        "audit-immutability proof. Skipped automatically when no database is configured.";

    private readonly AuditLogDatabaseFixture _fixture;

    public AuditLogImmutabilityTests(AuditLogDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Audit_table_should_allow_insert()
    {
        Skip.IfNot(_fixture.IsConfigured, SkipReason);

        await using var db = _fixture.CreateDbContext();

        var entry = CreateAuditEntry(
            endpoint: "/admin/license/current",
            actionCategory: "Read");

        db.Set<AuditLogEntry>().Add(entry);
        await db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [SkippableFact]
    public async Task Audit_table_should_allow_select()
    {
        Skip.IfNot(_fixture.IsConfigured, SkipReason);

        await using var db = _fixture.CreateDbContext();

        var entry = CreateAuditEntry(
            endpoint: "/admin/jobs-monitor",
            actionCategory: "Read");

        db.Set<AuditLogEntry>().Add(entry);
        await db.SaveChangesAsync();

        var loaded = await db
            .Set<AuditLogEntry>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entry.Id);

        Assert.NotNull(loaded);
        Assert.Equal(entry.Id, loaded!.Id);
        Assert.Equal("Success", loaded.OutcomeStatus);
    }

    [SkippableFact]
    public async Task Audit_table_should_block_update()
    {
        Skip.IfNot(_fixture.IsConfigured, SkipReason);

        Guid insertedId;

        await using (var db = _fixture.CreateDbContext())
        {
            var entry = CreateAuditEntry(
                endpoint: "/admin/schema-configuration/views",
                actionCategory: "Create");

            db.Set<AuditLogEntry>().Add(entry);
            await db.SaveChangesAsync();

            insertedId = entry.Id;
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
                await db.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE audit_log_entries
                    SET outcome_status = 'Tampered'
                    WHERE id = {0}
                    """,
                    insertedId));

            AssertMutationBlocked(exception);
        }

        await using (var verifyDb = _fixture.CreateDbContext())
        {
            var row = await verifyDb
                .Set<AuditLogEntry>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == insertedId);

            Assert.NotNull(row);
            Assert.Equal("Success", row!.OutcomeStatus);
        }
    }

    [SkippableFact]
    public async Task Audit_table_should_block_delete()
    {
        Skip.IfNot(_fixture.IsConfigured, SkipReason);

        Guid insertedId;

        await using (var db = _fixture.CreateDbContext())
        {
            var entry = CreateAuditEntry(
                endpoint: "/admin/db-configuration/summary",
                actionCategory: "Read");

            db.Set<AuditLogEntry>().Add(entry);
            await db.SaveChangesAsync();

            insertedId = entry.Id;
        }

        await using var mutationDb = _fixture.CreateDbContext();

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
            await mutationDb.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM audit_log_entries
                WHERE id = {0}
                """,
                insertedId));

        AssertMutationBlocked(exception);

        await using var verifyDb = _fixture.CreateDbContext();

        var stillExists = await verifyDb
            .Set<AuditLogEntry>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == insertedId);

        Assert.True(stillExists);
    }

    [SkippableFact]
    public async Task Audit_table_should_block_truncate()
    {
        Skip.IfNot(_fixture.IsConfigured, SkipReason);

        await using var db = _fixture.CreateDbContext();

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE audit_log_entries"));

        AssertMutationBlocked(exception);
    }

    private static AuditLogEntry CreateAuditEntry(
        string endpoint,
        string actionCategory)
    {
        return new AuditLogEntry(
            httpMethod: "GET",
            endpoint: endpoint,
            actionCategory: actionCategory,
            outcomeStatus: "Success",
            userId: "hardening-test-user",
            userName: "hardening-test-user",
            resourceType: "HardeningTest",
            resourceId: Guid.NewGuid().ToString(),
            clientIp: "127.0.0.1",
            userAgent: "PlantProcessIQ.HardeningTests",
            correlationId: Guid.NewGuid().ToString(),
            httpStatusCode: 200,
            metadataJson: """
            {
              "source": "AuditLogImmutabilityTests",
              "task": "BE-ADD-001",
              "mode": "local-trigger-guard"
            }
            """);
    }

    private static void AssertMutationBlocked(PostgresException exception)
    {
        Assert.True(
            exception.SqlState == "P0001" ||
            exception.SqlState == PostgresErrorCodes.InsufficientPrivilege,
            $"Expected audit mutation to be blocked by trigger P0001 or privilege 42501, but got SQLSTATE {exception.SqlState}: {exception.MessageText}");
    }
}

public sealed class AuditLogDatabaseFixture
{
    private const string ConnectionStringEnvironmentVariable =
        "PPIQ_AUDIT_TRIGGER_TEST_CONNECTION";

    public AuditLogDatabaseFixture()
    {
        // Do NOT throw here. A missing connection string simply means the
        // opt-in audit-immutability tests are skipped (see Skip.IfNot in the
        // test methods). Throwing would surface as test FAILURES instead of
        // skips and would break `dotnet test` in CI and on fresh machines.
        ConnectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
    }

    public string? ConnectionString { get; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    public PlantProcessDbContext CreateDbContext()
    {
        if (!IsConfigured)
        {
            // Defensive guard: tests gate on IsConfigured via Skip.IfNot before
            // ever calling this, so this should be unreachable in practice.
            throw new InvalidOperationException(
                $"{ConnectionStringEnvironmentVariable} is not set. " +
                "CreateDbContext() must not be called when the fixture is not configured.");
        }

        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .EnableSensitiveDataLogging(false)
            .Options;

        return new PlantProcessDbContext(options);
    }
}
