\set ON_ERROR_STOP on
\qecho PPIQ_PSQL_EXECUTED accepted_record_store
BEGIN;

-- Runtime-owned fencing observations are installed only by the commissioned session
-- authority. The application role cannot create or replace them. Takeover and store
-- writes MUST acquire this same row before locking a batch (fixed lock order).
CREATE TABLE IF NOT EXISTS ppiq_staging.accepted_capture_fences (
    tenant_id uuid NOT NULL,
    dataset_governance_id uuid NOT NULL,
    stream_key text NOT NULL CHECK (length(stream_key) BETWEEN 1 AND 200),
    session_id uuid NOT NULL,
    generation bigint NOT NULL CHECK (generation > 0),
    valid_until_utc timestamptz NOT NULL,
    committed_ordinal bigint NOT NULL DEFAULT 0 CHECK (committed_ordinal >= 0),
    committed_position text,
    PRIMARY KEY (tenant_id, dataset_governance_id, stream_key),
    FOREIGN KEY (tenant_id, dataset_governance_id)
        REFERENCES ppiq_meta.source_dataset_governance(tenant_id,id) ON DELETE RESTRICT
);
CREATE TABLE IF NOT EXISTS ppiq_staging.accepted_batches (
    tenant_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    dataset_governance_id uuid NOT NULL,
    stream_key text NOT NULL,
    batch_ordinal bigint NOT NULL CHECK (batch_ordinal > 0),
    session_id uuid NOT NULL,
    generation bigint NOT NULL CHECK (generation > 0),
    configuration_id uuid NOT NULL,
    configuration_version integer NOT NULL,
    configuration_version_id uuid NOT NULL REFERENCES ppiq_meta.definition_versions(id) ON DELETE RESTRICT,
    recording_group_key text NOT NULL,
    field_shape jsonb NOT NULL,
    preservation_required boolean NOT NULL,
    maximum_records bigint NOT NULL CHECK (maximum_records BETWEEN 1 AND 1000000),
    maximum_bytes bigint NOT NULL CHECK (maximum_bytes BETWEEN 1 AND 1073741824),
    start_position text,
    end_position text,
    state text NOT NULL DEFAULT 'Open' CHECK (state IN ('Open','Sealed')),
    record_count bigint NOT NULL DEFAULT 0 CHECK (record_count >= 0),
    payload_bytes bigint NOT NULL DEFAULT 0 CHECK (payload_bytes >= 0),
    relation_name text,
    opened_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
    sealed_at_utc timestamptz,
    PRIMARY KEY (tenant_id,batch_id),
    CONSTRAINT uq_accepted_batches_stream_ordinal UNIQUE (tenant_id,dataset_governance_id,stream_key,batch_ordinal),
    FOREIGN KEY (tenant_id,dataset_governance_id,stream_key)
        REFERENCES ppiq_staging.accepted_capture_fences(tenant_id,dataset_governance_id,stream_key) ON DELETE RESTRICT,
    CHECK (record_count <= maximum_records AND payload_bytes <= maximum_bytes),
    CHECK ((state='Open' AND relation_name IS NULL AND sealed_at_utc IS NULL)
        OR (state='Sealed' AND relation_name IS NOT NULL AND sealed_at_utc IS NOT NULL))
);
CREATE TABLE IF NOT EXISTS ppiq_staging.accepted_records (
    tenant_id uuid NOT NULL,
    dataset_governance_id uuid NOT NULL,
    stream_key text NOT NULL,
    record_id text NOT NULL CHECK (length(record_id) BETWEEN 1 AND 200),
    batch_id uuid NOT NULL,
    receipt_id uuid NOT NULL DEFAULT gen_random_uuid(),
    durable_position bigint GENERATED ALWAYS AS IDENTITY,
    envelope jsonb NOT NULL CHECK (jsonb_typeof(envelope)='object'),
    content_hash text NOT NULL CHECK (content_hash ~ '^[0-9a-f]{64}$'),
    payload_bytes bigint NOT NULL CHECK (payload_bytes > 0),
    accepted_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (tenant_id,dataset_governance_id,stream_key,record_id),
    UNIQUE (receipt_id),
    FOREIGN KEY (tenant_id,batch_id) REFERENCES ppiq_staging.accepted_batches(tenant_id,batch_id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_accepted_records_batch ON ppiq_staging.accepted_records(tenant_id,batch_id,durable_position);
-- Receipts are columns of the accepted row: there is no receipt payload copy and
-- no opportunity for a committed receipt with an uncommitted record.
CREATE TABLE IF NOT EXISTS ppiq_staging.accepted_integrity_conflicts (
    conflict_id uuid NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tenant_id uuid NOT NULL,
    receipt_id uuid NOT NULL REFERENCES ppiq_staging.accepted_records(receipt_id) ON DELETE RESTRICT,
    attempted_hash text NOT NULL,
    observed_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
    UNIQUE(tenant_id,receipt_id,attempted_hash)
);

-- Even owner-created views retain an explicit tenant filter. Direct table reads
-- by the runtime role additionally use forced RLS.
DO $rls$
DECLARE n text;
BEGIN
 FOREACH n IN ARRAY ARRAY['accepted_capture_fences','accepted_batches','accepted_records','accepted_integrity_conflicts'] LOOP
  EXECUTE format('ALTER TABLE ppiq_staging.%I ENABLE ROW LEVEL SECURITY',n);
  EXECUTE format('ALTER TABLE ppiq_staging.%I FORCE ROW LEVEL SECURITY',n);
  EXECUTE format('DROP POLICY IF EXISTS accepted_tenant ON ppiq_staging.%I',n);
  EXECUTE format('CREATE POLICY accepted_tenant ON ppiq_staging.%I USING (tenant_id = nullif(current_setting(''app.current_tenant'',true),'''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.current_tenant'',true),'''')::uuid)',n);
 END LOOP;
END $rls$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_assert_tenant(p_tenant uuid)
RETURNS void LANGUAGE plpgsql SET search_path=pg_catalog,ppiq_staging AS $f$
BEGIN
 IF p_tenant IS NULL OR p_tenant IS DISTINCT FROM nullif(current_setting('app.current_tenant',true),'')::uuid THEN
  RAISE EXCEPTION 'AR01 tenant_scope_required' USING ERRCODE='P0001';
 END IF;
END $f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_lock_fence(p_tenant uuid,p_dataset uuid,p_stream text,p_session uuid,p_generation bigint)
RETURNS void LANGUAGE plpgsql SET search_path=pg_catalog,ppiq_staging AS $f$
DECLARE f ppiq_staging.accepted_capture_fences%ROWTYPE;
BEGIN
 PERFORM ppiq_staging.accepted_assert_tenant(p_tenant);
 SELECT * INTO f FROM ppiq_staging.accepted_capture_fences
  WHERE tenant_id=p_tenant AND dataset_governance_id=p_dataset AND stream_key=p_stream FOR UPDATE;
 IF NOT FOUND OR f.session_id IS DISTINCT FROM p_session OR f.generation IS DISTINCT FROM p_generation
     OR f.valid_until_utc <= clock_timestamp() THEN
  RAISE EXCEPTION 'AR04 capture_generation_fenced' USING ERRCODE='P0001';
 END IF;
END $f$;

-- A single mapping of declared types governs validation and relation projection.
CREATE OR REPLACE FUNCTION ppiq_staging.accepted_sql_type(p_type text)
RETURNS text LANGUAGE plpgsql IMMUTABLE SET search_path=pg_catalog AS $f$
BEGIN
 RETURN CASE p_type
  WHEN 'boolean' THEN 'boolean'
  WHEN 'int8' THEN 'smallint' WHEN 'uint8' THEN 'smallint' WHEN 'int16' THEN 'smallint'
  WHEN 'uint16' THEN 'integer' WHEN 'int32' THEN 'integer'
  WHEN 'uint32' THEN 'bigint' WHEN 'int64' THEN 'bigint' WHEN 'uint64' THEN 'numeric(20,0)'
  WHEN 'float32' THEN 'real' WHEN 'float64' THEN 'double precision' WHEN 'decimal' THEN 'numeric'
  WHEN 'string' THEN 'text' WHEN 'bytes' THEN 'bytea' WHEN 'datetime' THEN 'timestamp with time zone'
  WHEN 'date' THEN 'date' WHEN 'time' THEN 'time without time zone'
  WHEN 'duration' THEN 'interval' WHEN 'guid' THEN 'uuid'
  ELSE NULL END;
END $f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_open(p_tenant uuid,p_dataset uuid,p_batch uuid,
 p_session uuid,p_generation bigint,p_stream text,p_ordinal bigint,p_configuration uuid,p_version integer,
 p_group text,p_max_records bigint,p_max_bytes bigint,p_start text)
RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging,ppiq_meta AS $f$
DECLARE b ppiq_staging.accepted_batches%ROWTYPE; v uuid; doc jsonb; shape jsonb; grp jsonb; fld jsonb;
 required boolean; key_count integer;
BEGIN
 PERFORM ppiq_staging.accepted_lock_fence(p_tenant,p_dataset,p_stream,p_session,p_generation);
 SELECT * INTO b FROM ppiq_staging.accepted_batches WHERE tenant_id=p_tenant AND batch_id=p_batch FOR UPDATE;
 IF FOUND THEN
  IF ROW(b.dataset_governance_id,b.session_id,b.generation,b.stream_key,b.batch_ordinal,b.configuration_id,
    b.configuration_version,b.recording_group_key,b.maximum_records,b.maximum_bytes,b.start_position)
   IS DISTINCT FROM ROW(p_dataset,p_session,p_generation,p_stream,p_ordinal,p_configuration,p_version,p_group,p_max_records,p_max_bytes,p_start) THEN
   RAISE EXCEPTION 'AR02 conflicting_batch_declaration' USING ERRCODE='P0001';
  END IF;
  RETURN b.batch_id;
 END IF;
 SELECT dv.id,dv.graph_json INTO v,doc FROM ppiq_meta.definition_versions dv
 JOIN ppiq_meta.definition_store ds ON ds.id=dv.definition_id
 JOIN ppiq_meta.acquisition_configuration_details d ON d.definition_version_id=dv.id
 WHERE dv.tenant_id=p_tenant AND ds.tenant_id=p_tenant AND ds.definition_kind='acquisition_configuration'
 AND dv.definition_id=p_configuration AND dv.version_number=p_version AND dv.status='published'
 AND NOT dv.is_deleted AND NOT ds.is_deleted AND d.dataset_governance_id=p_dataset;
 IF v IS NULL THEN RAISE EXCEPTION 'AR03 exact_published_configuration_required' USING ERRCODE='P0001'; END IF;
 SELECT g INTO grp FROM jsonb_array_elements(doc->'recordingGroups') g WHERE g->>'groupKey'=p_group;
 IF grp IS NULL THEN RAISE EXCEPTION 'AR03 recording_group_unknown' USING ERRCODE='P0001'; END IF;
 SELECT jsonb_agg(jsonb_build_object('fieldId',r.field_id,'revision',r.revision,'key',r.field_key,
  'type',r.declared_type,'typeShape',r.type_shape,'unit',r.source_unit) ORDER BY r.field_key),count(*)
 INTO shape,key_count FROM jsonb_array_elements(doc->'fields') x
 JOIN ppiq_meta.source_field_revisions r ON r.field_id=(x->>'fieldId')::uuid AND r.revision=(x->>'revision')::integer AND r.tenant_id=p_tenant
 JOIN ppiq_meta.source_field_identities i ON i.field_id=r.field_id AND i.tenant_id=p_tenant AND i.dataset_governance_id=p_dataset
 WHERE (grp->'memberFieldIds') ? (r.field_id::text);
 IF shape IS NULL OR key_count > 1596 OR key_count <> jsonb_array_length(grp->'memberFieldIds') THEN
  RAISE EXCEPTION 'AR12 governed_field_shape_required' USING ERRCODE='P0001'; END IF;
 FOR fld IN SELECT * FROM jsonb_array_elements(shape) LOOP
  IF fld->>'key' !~ '^[a-z_][a-z0-9_]{0,62}$' OR fld->>'key' IN ('source_system','source_record_id','batch_id','configuration_version')
   OR ppiq_staging.accepted_sql_type(fld->>'type') IS NULL OR (fld->'typeShape') ? 'arrayLength' THEN
   RAISE EXCEPTION 'AR12 unsupported_relation_field' USING ERRCODE='P0001'; END IF;
 END LOOP;
 IF (SELECT count(DISTINCT x->>'key') FROM jsonb_array_elements(shape) x) <> key_count THEN
  RAISE EXCEPTION 'AR12 duplicate_field_key' USING ERRCODE='P0001'; END IF;
 -- The current configuration declares a retentionPolicyRef, not a preservation
 -- boolean. Until that policy authority is integrated, a referenced retention
 -- policy is unresolved and acceptance fails closed. Never invent a JSON member.
 required := (doc->'storage'->>'retentionPolicyRef') IS NOT NULL;
 IF p_ordinal <= (SELECT committed_ordinal FROM ppiq_staging.accepted_capture_fences
  WHERE tenant_id=p_tenant AND dataset_governance_id=p_dataset AND stream_key=p_stream) THEN
  RAISE EXCEPTION 'AR10 batch_ordinal_already_committed' USING ERRCODE='P0001'; END IF;
 INSERT INTO ppiq_staging.accepted_batches(tenant_id,batch_id,dataset_governance_id,stream_key,batch_ordinal,
 session_id,generation,configuration_id,configuration_version,configuration_version_id,recording_group_key,
 field_shape,preservation_required,maximum_records,maximum_bytes,start_position)
 VALUES(p_tenant,p_batch,p_dataset,p_stream,p_ordinal,p_session,p_generation,p_configuration,p_version,v,p_group,
 shape,required,p_max_records,p_max_bytes,p_start);
 RETURN p_batch;
END $f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_append(p_tenant uuid,p_batch uuid,p_session uuid,
 p_generation bigint,p_record text,p_envelope jsonb)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging,ppiq_meta AS $f$
DECLARE b ppiq_staging.accepted_batches%ROWTYPE; r ppiq_staging.accepted_records%ROWTYPE;
 h text; bytes bigint; fld jsonb; val jsonb; typ text; n numeric; k text;
BEGIN
 PERFORM ppiq_staging.accepted_assert_tenant(p_tenant);
 SELECT * INTO b FROM ppiq_staging.accepted_batches WHERE tenant_id=p_tenant AND batch_id=p_batch;
 IF NOT FOUND THEN RAISE EXCEPTION 'AR02 batch_unknown' USING ERRCODE='P0001'; END IF;
 PERFORM ppiq_staging.accepted_lock_fence(p_tenant,b.dataset_governance_id,b.stream_key,p_session,p_generation);
 SELECT * INTO b FROM ppiq_staging.accepted_batches WHERE tenant_id=p_tenant AND batch_id=p_batch FOR UPDATE;
 IF ROW(b.session_id,b.generation) IS DISTINCT FROM ROW(p_session,p_generation) THEN
  RAISE EXCEPTION 'AR04 batch_generation_fenced' USING ERRCODE='P0001'; END IF;
 IF p_envelope IS NULL OR jsonb_typeof(p_envelope) <> 'object' OR p_record IS NULL OR length(p_record) NOT BETWEEN 1 AND 200 THEN
  RAISE EXCEPTION 'AR11 invalid_record_envelope' USING ERRCODE='P0001'; END IF;
 h := encode(public.digest(convert_to(p_envelope::text,'UTF8'),'sha256'),'hex');
 SELECT * INTO r FROM ppiq_staging.accepted_records WHERE tenant_id=p_tenant
  AND dataset_governance_id=b.dataset_governance_id AND stream_key=b.stream_key AND record_id=p_record;
 IF FOUND THEN
  IF r.envelope IS DISTINCT FROM p_envelope THEN
   -- Return a refusal so the caller can commit the incident without accepting data.
   INSERT INTO ppiq_staging.accepted_integrity_conflicts(tenant_id,receipt_id,attempted_hash)
    VALUES(p_tenant,r.receipt_id,h) ON CONFLICT DO NOTHING;
   RETURN jsonb_build_object('code','AR07','detail','Immutable record identity has conflicting content.');
  END IF;
  RETURN to_jsonb(r)-'envelope';
 END IF;
 IF b.state <> 'Open' THEN RAISE EXCEPTION 'AR05 batch_is_sealed' USING ERRCODE='P0001'; END IF;
 -- Preservation has no commissioned database authority yet. Required policies must
 -- refuse even if the envelope supplies convincing metadata. No boolean bypass.
 IF b.preservation_required THEN RAISE EXCEPTION 'AR14 preservation_authority_unavailable' USING ERRCODE='P0001'; END IF;
 IF jsonb_typeof(p_envelope->'values') IS DISTINCT FROM 'object'
    OR NOT (p_envelope ?& ARRAY['capturedAtUtc','quality','consistency','conversionVersion']) THEN
  RAISE EXCEPTION 'AR11 incomplete_record_envelope' USING ERRCODE='P0001'; END IF;
 IF coalesce(p_envelope->>'capturedAtUtc','') !~ '(Z|[+-][0-9]{2}:[0-9]{2})$'
  OR coalesce(p_envelope->>'conversionVersion','')='' OR coalesce(p_envelope->>'consistency','') NOT IN
   ('IndependentObservations','BoundedReadWindow','TemporallyAligned','SourceVersionVerified','SourceLatchedRecord') THEN
  RAISE EXCEPTION 'AR11 invalid_capture_metadata' USING ERRCODE='P0001'; END IF;
 PERFORM (p_envelope->>'capturedAtUtc')::timestamptz;
 IF (SELECT count(*) FROM jsonb_object_keys(p_envelope->'values')) <> jsonb_array_length(b.field_shape) THEN
  RAISE EXCEPTION 'AR12 field_members_do_not_match_configuration' USING ERRCODE='P0001'; END IF;
 FOR fld IN SELECT * FROM jsonb_array_elements(b.field_shape) LOOP
  k:=fld->>'fieldId'; val:=p_envelope->'values'->k; typ:=fld->>'type';
  IF val IS NULL THEN RAISE EXCEPTION 'AR12 missing_declared_field' USING ERRCODE='P0001'; END IF;
  IF jsonb_typeof(val)='null' THEN CONTINUE; END IF;
  IF typ IN ('int8','uint8','int16','uint16','int32','uint32','int64','uint64','decimal','float32','float64') THEN
   IF jsonb_typeof(val)<>'number' THEN RAISE EXCEPTION 'AR12 numeric_field_requires_number' USING ERRCODE='P0001'; END IF;
   n:=(val#>>'{}')::numeric;
   IF typ IN ('int8','uint8','int16','uint16','int32','uint32','int64','uint64') THEN
    -- Parenthesize CASE expressions inside IF so PL/pgSQL reads their THEN tokens as SQL.
    IF n<>trunc(n) OR n < (CASE typ WHEN 'int8' THEN -128 WHEN 'int16' THEN -32768 WHEN 'int32' THEN -2147483648 WHEN 'int64' THEN -9223372036854775808 ELSE 0 END)
     OR n > (CASE typ WHEN 'int8' THEN 127 WHEN 'uint8' THEN 255 WHEN 'int16' THEN 32767 WHEN 'uint16' THEN 65535 WHEN 'int32' THEN 2147483647 WHEN 'uint32' THEN 4294967295 WHEN 'int64' THEN 9223372036854775807 ELSE 18446744073709551615 END) THEN
     RAISE EXCEPTION 'AR12 integer_out_of_range' USING ERRCODE='P0001'; END IF;
   END IF;
  ELSIF typ='boolean' THEN
   IF jsonb_typeof(val)<>'boolean' THEN RAISE EXCEPTION 'AR12 boolean_required' USING ERRCODE='P0001'; END IF;
  ELSIF jsonb_typeof(val)<>'string' THEN RAISE EXCEPTION 'AR12 string_representation_required' USING ERRCODE='P0001'; END IF;
  IF typ='decimal' AND (fld->'typeShape') ? 'precision' THEN
   IF n<>round(n,(fld->'typeShape'->>'scale')::integer)
    OR abs(n)>=power(10::numeric,(fld->'typeShape'->>'precision')::integer-(fld->'typeShape'->>'scale')::integer) THEN
    RAISE EXCEPTION 'AR12 decimal_shape_exceeded' USING ERRCODE='P0001'; END IF;
  END IF;
  IF typ='string' AND length(val#>>'{}')>coalesce((fld->'typeShape'->>'maxLength')::integer,1048576) THEN
   RAISE EXCEPTION 'AR12 string_length_exceeded' USING ERRCODE='P0001'; END IF;
  IF typ='bytes' THEN
   IF octet_length(decode(val#>>'{}','base64'))>coalesce((fld->'typeShape'->>'maxLength')::integer,1048576) THEN
    RAISE EXCEPTION 'AR12 bytes_length_exceeded' USING ERRCODE='P0001'; END IF;
  ELSE EXECUTE format('SELECT ($1)::%s',ppiq_staging.accepted_sql_type(typ)) USING val#>>'{}'; END IF;
 END LOOP;
 bytes:=octet_length(convert_to(p_envelope::text,'UTF8'));
 IF b.record_count>=b.maximum_records OR b.payload_bytes+bytes>b.maximum_bytes THEN
  RAISE EXCEPTION 'AR11 finite_batch_capacity_exceeded' USING ERRCODE='P0001'; END IF;
 INSERT INTO ppiq_staging.accepted_records(tenant_id,dataset_governance_id,stream_key,record_id,batch_id,envelope,content_hash,payload_bytes)
 VALUES(p_tenant,b.dataset_governance_id,b.stream_key,p_record,p_batch,p_envelope,h,bytes) RETURNING * INTO r;
 UPDATE ppiq_staging.accepted_batches SET record_count=record_count+1,payload_bytes=payload_bytes+bytes
 WHERE tenant_id=p_tenant AND batch_id=p_batch;
 RETURN to_jsonb(r)-'envelope';
END $f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_seal(p_tenant uuid,p_batch uuid,p_session uuid,p_generation bigint,
 p_count bigint,p_bytes bigint,p_end text)
RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging,ppiq_meta AS $f$
DECLARE b ppiq_staging.accepted_batches%ROWTYPE; fld jsonb; cols text:=''; expr text; rel text;
 f ppiq_staging.accepted_capture_fences%ROWTYPE; successor ppiq_staging.accepted_batches%ROWTYPE;
BEGIN
 PERFORM ppiq_staging.accepted_assert_tenant(p_tenant);
 SELECT * INTO b FROM ppiq_staging.accepted_batches WHERE tenant_id=p_tenant AND batch_id=p_batch;
 IF NOT FOUND THEN RAISE EXCEPTION 'AR02 batch_unknown' USING ERRCODE='P0001'; END IF;
 PERFORM ppiq_staging.accepted_lock_fence(p_tenant,b.dataset_governance_id,b.stream_key,p_session,p_generation);
 SELECT * INTO b FROM ppiq_staging.accepted_batches WHERE tenant_id=p_tenant AND batch_id=p_batch FOR UPDATE;
 IF ROW(b.session_id,b.generation) IS DISTINCT FROM ROW(p_session,p_generation) THEN
  RAISE EXCEPTION 'AR04 batch_generation_fenced' USING ERRCODE='P0001'; END IF;
 IF b.state='Sealed' THEN
  IF ROW(b.record_count,b.payload_bytes,b.end_position) IS DISTINCT FROM ROW(p_count,p_bytes,p_end) THEN
   RAISE EXCEPTION 'AR08 conflicting_seal' USING ERRCODE='P0001'; END IF;
  RETURN p_batch;
 END IF;
 IF ROW(b.record_count,b.payload_bytes) IS DISTINCT FROM ROW(p_count,p_bytes) THEN
  RAISE EXCEPTION 'AR09 seal_summary_mismatch' USING ERRCODE='P0001'; END IF;
 FOR fld IN SELECT * FROM jsonb_array_elements(b.field_shape) LOOP
  expr:=format('(r.envelope->''values''->>%L)',fld->>'fieldId');
  IF fld->>'type'='bytes' THEN expr:=format('decode(%s,''base64'')',expr);
  ELSE expr:=format('(%s)::%s',expr,ppiq_staging.accepted_sql_type(fld->>'type')); END IF;
  cols:=cols || format(',%s AS %I',expr,fld->>'key');
 END LOOP;
 rel:='accepted_batch_'||replace(p_batch::text,'-','');
 -- Exact immutable batch relation. No CREATE OR REPLACE: name collision is drift.
 EXECUTE format('CREATE VIEW ppiq_staging.%I WITH (security_barrier=true) AS SELECT
  %L::text AS source_system,r.record_id::text AS source_record_id,r.batch_id,%s::integer AS configuration_version %s
  FROM ppiq_staging.accepted_records r JOIN ppiq_staging.accepted_batches b
   ON b.tenant_id=r.tenant_id AND b.batch_id=r.batch_id
  WHERE r.tenant_id=%L::uuid AND r.tenant_id=nullif(current_setting(''app.current_tenant'',true),'''')::uuid
   AND r.batch_id=%L::uuid AND b.state=''Sealed''',rel,'accepted:'||encode(public.digest(convert_to(p_tenant::text||'/'||b.dataset_governance_id::text||'/'||b.stream_key,'UTF8'),'sha256'),'hex'),b.configuration_version,cols,p_tenant,p_batch);
 IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='plantprocess_app') THEN
  EXECUTE format('GRANT SELECT ON ppiq_staging.%I TO plantprocess_app',rel);
 END IF;
 UPDATE ppiq_staging.accepted_batches SET state='Sealed',end_position=p_end,relation_name=rel,sealed_at_utc=clock_timestamp()
 WHERE tenant_id=p_tenant AND batch_id=p_batch;
 SELECT * INTO f FROM ppiq_staging.accepted_capture_fences WHERE tenant_id=p_tenant
 AND dataset_governance_id=b.dataset_governance_id AND stream_key=b.stream_key;
 LOOP
  SELECT * INTO successor FROM ppiq_staging.accepted_batches WHERE tenant_id=p_tenant
   AND dataset_governance_id=b.dataset_governance_id AND stream_key=b.stream_key
   AND batch_ordinal=f.committed_ordinal+1 AND state='Sealed';
  EXIT WHEN NOT FOUND;
  -- Positions are opaque tokens: equality validates adjacency; no invented ordering.
  IF f.committed_ordinal>0 AND successor.start_position IS DISTINCT FROM f.committed_position THEN
   RAISE EXCEPTION 'AR10 checkpoint_boundary_conflict' USING ERRCODE='P0001'; END IF;
  f.committed_ordinal:=successor.batch_ordinal; f.committed_position:=successor.end_position;
 END LOOP;
 UPDATE ppiq_staging.accepted_capture_fences SET committed_ordinal=f.committed_ordinal,committed_position=f.committed_position
 WHERE tenant_id=p_tenant AND dataset_governance_id=b.dataset_governance_id AND stream_key=b.stream_key;
 RETURN p_batch;
END $f$;

-- The app can invoke governed mutations and read its tenant. It cannot fabricate
-- fences or directly mutate payloads, counters, receipts, summaries or checkpoints.
REVOKE ALL ON ppiq_staging.accepted_capture_fences,ppiq_staging.accepted_batches,
 ppiq_staging.accepted_records,ppiq_staging.accepted_integrity_conflicts FROM PUBLIC;
REVOKE ALL ON FUNCTION ppiq_staging.accepted_open(uuid,uuid,uuid,uuid,bigint,text,bigint,uuid,integer,text,bigint,bigint,text) FROM PUBLIC;
REVOKE ALL ON FUNCTION ppiq_staging.accepted_append(uuid,uuid,uuid,bigint,text,jsonb) FROM PUBLIC;
REVOKE ALL ON FUNCTION ppiq_staging.accepted_seal(uuid,uuid,uuid,bigint,bigint,bigint,text) FROM PUBLIC;
DO $grants$
BEGIN
 IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='plantprocess_app') THEN
  REVOKE ALL ON ppiq_staging.accepted_capture_fences,ppiq_staging.accepted_batches,
   ppiq_staging.accepted_records,ppiq_staging.accepted_integrity_conflicts FROM plantprocess_app;
  GRANT SELECT ON ppiq_staging.accepted_capture_fences,ppiq_staging.accepted_batches TO plantprocess_app;
  GRANT EXECUTE ON FUNCTION ppiq_staging.accepted_open(uuid,uuid,uuid,uuid,bigint,text,bigint,uuid,integer,text,bigint,bigint,text) TO plantprocess_app;
  GRANT EXECUTE ON FUNCTION ppiq_staging.accepted_append(uuid,uuid,uuid,bigint,text,jsonb) TO plantprocess_app;
  GRANT EXECUTE ON FUNCTION ppiq_staging.accepted_seal(uuid,uuid,uuid,bigint,bigint,bigint,text) TO plantprocess_app;
 END IF;
END $grants$;
COMMIT;
