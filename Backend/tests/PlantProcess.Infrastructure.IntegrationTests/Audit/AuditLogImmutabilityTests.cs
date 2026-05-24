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
// Required environment variable:
//   PPIQ_AUDIT_TRIGGER_TEST_CONNECTION
//
// Example:
//   Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=YOUR_PASSWORD
//
// Expected SQLSTATE:
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
    private readonly AuditLogDatabaseFixture _fixture;

    public AuditLogImmutabilityTests(AuditLogDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Audit_table_should_allow_insert()
    {
        await using var db = _fixture.CreateDbContext();

        var entry = CreateAuditEntry(
            endpoint: "/admin/license/current",
            actionCategory: "Read");

        db.Set<AuditLogEntry>().Add(entry);
        await db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [Fact]
    public async Task Audit_table_should_allow_select()
    {
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

    [Fact]
    public async Task Audit_table_should_block_update()
    {
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

    [Fact]
    public async Task Audit_table_should_block_delete()
    {
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

    [Fact]
    public async Task Audit_table_should_block_truncate()
    {
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
        ConnectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Missing environment variable {ConnectionStringEnvironmentVariable}. " +
                "Set it to your local PostgreSQL connection string, for example: " +
                "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=YOUR_PASSWORD");
    }

    public string ConnectionString { get; }

    public PlantProcessDbContext CreateDbContext()
    {
       var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .EnableSensitiveDataLogging(false)
            .Options;

        return new PlantProcessDbContext(options);
    }
}