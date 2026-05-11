-- PlantProcess IQ - Phase C manual migration fallback
-- Use this only if you do not generate an EF Core migration with:
-- dotnet ef migrations add AddStagingRecordEntity --project PlantProcess.Infrastructure --startup-project PlantProcess.Api

BEGIN;

CREATE TABLE IF NOT EXISTS staging_records
(
    id uuid PRIMARY KEY,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NULL,
    is_synthetic boolean NOT NULL,
    source_system varchar(100) NULL,
    source_record_id varchar(100) NULL,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at_utc timestamp with time zone NULL,
    deleted_reason varchar(500) NULL,

    import_batch_id uuid NOT NULL,
    source_object_name varchar(200) NOT NULL,
    row_number integer NOT NULL,
    raw_json jsonb NOT NULL,
    is_processed boolean NOT NULL DEFAULT false,
    processed_at_utc timestamp with time zone NULL,
    processing_status varchar(50) NOT NULL DEFAULT 'Pending',
    processing_error varchar(4000) NULL,
    canonical_entity_id uuid NULL,
    canonical_entity_name varchar(200) NULL,

    CONSTRAINT fk_staging_records_import_batches_import_batch_id
        FOREIGN KEY (import_batch_id)
        REFERENCES import_batches (id)
        ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_staging_records_import_batch_source_row
    ON staging_records (import_batch_id, source_object_name, row_number);

CREATE INDEX IF NOT EXISTS ix_staging_records_import_batch_id
    ON staging_records (import_batch_id);

CREATE INDEX IF NOT EXISTS ix_staging_records_import_batch_is_processed
    ON staging_records (import_batch_id, is_processed);

CREATE INDEX IF NOT EXISTS ix_staging_records_import_batch_processing_status
    ON staging_records (import_batch_id, processing_status);

CREATE INDEX IF NOT EXISTS ix_staging_records_canonical_entity_id
    ON staging_records (canonical_entity_id);

COMMIT;
