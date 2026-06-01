using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Security;

public static class P01P02SecuritySchema
{
    public static async Task ApplyAsync(
        PlantProcessDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS tenants (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_code text NOT NULL UNIQUE,
    display_name text NOT NULL,
    environment_name text NOT NULL DEFAULT 'Demo',
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

INSERT INTO tenants (id, tenant_code, display_name, environment_name, is_active)
VALUES ('00000000-0000-0000-0000-000000000001', 'default-demo', 'Default Demo Tenant', 'Demo', true)
ON CONFLICT (id) DO NOTHING;

CREATE TABLE IF NOT EXISTS app_users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    user_name text NOT NULL,
    normalized_user_name text NOT NULL,
    display_name text NULL,
    password_hash text NOT NULL,
    password_salt text NOT NULL,
    password_iterations integer NOT NULL DEFAULT 210000,
    plant_role text NOT NULL,
    compatibility_role text NOT NULL,
    is_owner boolean NOT NULL DEFAULT false,
    is_enabled boolean NOT NULL DEFAULT true,
    force_password_change boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    CONSTRAINT ux_app_users_tenant_user UNIQUE (tenant_id, normalized_user_name)
);

CREATE TABLE IF NOT EXISTS auth_refresh_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL REFERENCES tenants(id),
    user_id uuid NOT NULL REFERENCES app_users(id),
    token_hash text NOT NULL UNIQUE,
    issued_at_utc timestamptz NOT NULL DEFAULT now(),
    expires_at_utc timestamptz NOT NULL,
    revoked_at_utc timestamptz NULL,
    replaced_by_token_hash text NULL,
    user_agent text NULL,
    client_ip text NULL
);

CREATE TABLE IF NOT EXISTS rbac_permission_matrix (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    plant_role text NOT NULL,
    permission_code text NOT NULL,
    is_allowed boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_rbac_permission_matrix UNIQUE (plant_role, permission_code)
);

INSERT INTO rbac_permission_matrix (plant_role, permission_code, is_allowed)
VALUES
('SuperAdmin','system.admin',true),
('SuperAdmin','tenant.admin',true),
('SuperAdmin','license.admin',true),
('SuperAdmin','user.admin',true),
('SuperAdmin','source.configure',true),
('SuperAdmin','mapping.configure',true),
('SuperAdmin','job.manage',true),
('SuperAdmin','page.design',true),
('SuperAdmin','widget.design',true),
('SuperAdmin','analysis.execute',true),
('SuperAdmin','suggestion.workflow',true),
('SuperAdmin','assistant.use',true),
('SuperAdmin','audit.read',true),
('SuperAdmin','report.export',true),
('TenantOwner','tenant.admin',true),
('TenantOwner','license.admin',true),
('TenantOwner','user.admin',true),
('TenantOwner','source.configure',true),
('TenantOwner','mapping.configure',true),
('TenantOwner','job.manage',true),
('TenantOwner','page.design',true),
('TenantOwner','widget.design',true),
('TenantOwner','analysis.execute',true),
('TenantOwner','suggestion.workflow',true),
('TenantOwner','assistant.use',true),
('TenantOwner','audit.read',true),
('TenantOwner','report.export',true),
('PlantAdmin','source.configure',true),
('PlantAdmin','mapping.configure',true),
('PlantAdmin','job.manage',true),
('PlantAdmin','page.design',true),
('PlantAdmin','widget.design',true),
('PlantAdmin','analysis.execute',true),
('PlantAdmin','suggestion.workflow',true),
('PlantAdmin','assistant.use',true),
('PlantAdmin','audit.read',true),
('PlantAdmin','report.export',true),
('DataEngineer','source.configure',true),
('DataEngineer','mapping.configure',true),
('DataEngineer','job.manage',true),
('DataEngineer','page.design',true),
('DataEngineer','widget.design',true),
('DataEngineer','analysis.execute',true),
('DataEngineer','report.export',true),
('ProcessEngineer','analysis.execute',true),
('ProcessEngineer','page.design',true),
('ProcessEngineer','widget.design',true),
('ProcessEngineer','suggestion.workflow',true),
('ProcessEngineer','assistant.use',true),
('ProcessEngineer','report.export',true),
('QualityEngineer','analysis.execute',true),
('QualityEngineer','page.design',true),
('QualityEngineer','widget.design',true),
('QualityEngineer','suggestion.workflow',true),
('QualityEngineer','assistant.use',true),
('QualityEngineer','report.export',true),
('ReliabilityEngineer','analysis.execute',true),
('ReliabilityEngineer','assistant.use',true),
('ReliabilityEngineer','report.export',true),
('Operator','analysis.execute',true),
('Viewer','assistant.use',true),
('CommercialAdmin','license.admin',true),
('Support','audit.read',true)
ON CONFLICT (plant_role, permission_code) DO UPDATE SET is_allowed = EXCLUDED.is_allowed;

DO $$
BEGIN
    IF to_regclass('ml_learning_results_v1') IS NOT NULL THEN
        ALTER TABLE ml_learning_results_v1 ADD COLUMN IF NOT EXISTS origin text NOT NULL DEFAULT 'computed';
        DELETE FROM ml_learning_results_v1
        WHERE lower(coalesce(origin, '')) = 'seed'
           OR lower(coalesce(model_name, '')) LIKE '%pearson%proof%'
           OR lower(coalesce(model_name, '')) LIKE '%spearman%proof%'
           OR lower(coalesce(model_version, '')) LIKE '%facade%'
           OR lower(coalesce(notes, '')) LIKE '%facade%'
           OR lower(coalesce(source_system, '')) LIKE '%synthetic%proof%';

        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_ml_learning_results_v1_origin') THEN
            ALTER TABLE ml_learning_results_v1
            ADD CONSTRAINT ck_ml_learning_results_v1_origin CHECK (origin IN ('seed','computed'));
        END IF;

        CREATE OR REPLACE VIEW v_ml_learning_results_live AS
        SELECT * FROM ml_learning_results_v1 WHERE origin = 'computed';
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('audit_log_entries') IS NOT NULL THEN
        ALTER TABLE audit_log_entries ADD COLUMN IF NOT EXISTS tenant_id uuid NULL;
        ALTER TABLE audit_log_entries ADD COLUMN IF NOT EXISTS before_snapshot_json jsonb NULL;
        ALTER TABLE audit_log_entries ADD COLUMN IF NOT EXISTS after_snapshot_json jsonb NULL;
        ALTER TABLE audit_log_entries ADD COLUMN IF NOT EXISTS previous_hash text NULL;
        ALTER TABLE audit_log_entries ADD COLUMN IF NOT EXISTS row_hash text NULL;

        UPDATE audit_log_entries
        SET tenant_id = '00000000-0000-0000-0000-000000000001'
        WHERE tenant_id IS NULL;

        CREATE OR REPLACE FUNCTION audit_log_entries_no_update_delete()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $fn$
        BEGIN
            RAISE EXCEPTION 'audit_log_entries is append-only; UPDATE/DELETE is forbidden';
        END;
        $fn$;

        DROP TRIGGER IF EXISTS trg_audit_log_entries_no_update ON audit_log_entries;
        CREATE TRIGGER trg_audit_log_entries_no_update
        BEFORE UPDATE ON audit_log_entries
        FOR EACH ROW EXECUTE FUNCTION audit_log_entries_no_update_delete();

        DROP TRIGGER IF EXISTS trg_audit_log_entries_no_delete ON audit_log_entries;
        CREATE TRIGGER trg_audit_log_entries_no_delete
        BEFORE DELETE ON audit_log_entries
        FOR EACH ROW EXECUTE FUNCTION audit_log_entries_no_update_delete();
    END IF;
END $$;
""";

        logger.LogInformation("Applying P01/P02 security schema hardening...");
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        logger.LogInformation("P01/P02 security schema hardening applied.");
    }
}
