\set ON_ERROR_STOP on
\qecho PPIQ_PSQL_EXECUTED accepted_dataset_contract
BEGIN;
ALTER TABLE ppiq_staging.accepted_capture_fences ADD COLUMN IF NOT EXISTS site_id uuid;
ALTER TABLE ppiq_staging.accepted_batches ADD COLUMN IF NOT EXISTS site_id uuid;
ALTER TABLE ppiq_staging.accepted_batches ADD COLUMN IF NOT EXISTS configuration_snapshot jsonb;
ALTER TABLE ppiq_staging.accepted_batches ADD COLUMN IF NOT EXISTS configuration_hash text;
UPDATE ppiq_staging.accepted_batches b SET configuration_snapshot=v.graph_json,configuration_hash=v.definition_hash
FROM ppiq_meta.definition_versions v WHERE v.id=b.configuration_version_id AND b.configuration_snapshot IS NULL;

CREATE TABLE IF NOT EXISTS ppiq_staging.accepted_source_artifacts(
 tenant_id uuid NOT NULL, artifact_id uuid NOT NULL, content bytea NOT NULL,
 sha256 text NOT NULL, media_type text NOT NULL, created_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
 PRIMARY KEY(tenant_id,artifact_id), CHECK(octet_length(content) BETWEEN 1 AND 16777216),
 CHECK(sha256=encode(public.digest(content,'sha256'),'hex')),
 CHECK(length(media_type) BETWEEN 1 AND 200));
CREATE TABLE IF NOT EXISTS ppiq_staging.accepted_artifact_admissions(
 tenant_id uuid NOT NULL, proof_id uuid NOT NULL, batch_id uuid NOT NULL, record_id text NOT NULL,
 artifact_id uuid NOT NULL, policy_reference text NOT NULL, policy_version text NOT NULL,
 retention_until_utc timestamptz NOT NULL,
 PRIMARY KEY(tenant_id,proof_id),
 FOREIGN KEY(tenant_id,batch_id) REFERENCES ppiq_staging.accepted_batches(tenant_id,batch_id),
 FOREIGN KEY(tenant_id,artifact_id) REFERENCES ppiq_staging.accepted_source_artifacts(tenant_id,artifact_id),
 CHECK(length(policy_reference)>0 AND length(policy_version)>0));
ALTER TABLE ppiq_staging.accepted_records ADD COLUMN IF NOT EXISTS original_bytes_proof_id uuid;
DO $constraint$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='ppiq_staging.accepted_records'::regclass AND conname='accepted_original_proof_fk') THEN
 ALTER TABLE ppiq_staging.accepted_records ADD CONSTRAINT accepted_original_proof_fk FOREIGN KEY(tenant_id,original_bytes_proof_id) REFERENCES ppiq_staging.accepted_artifact_admissions(tenant_id,proof_id);
 END IF;
END $constraint$;
CREATE TABLE IF NOT EXISTS ppiq_staging.accepted_gaps(
 tenant_id uuid NOT NULL, batch_id uuid NOT NULL, gap_id uuid NOT NULL,
 reason text NOT NULL CHECK(length(reason) BETWEEN 1 AND 200),
 from_position text, to_position text, missing_count bigint CHECK(missing_count>=0),
 observed_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
 PRIMARY KEY(tenant_id,gap_id), FOREIGN KEY(tenant_id,batch_id) REFERENCES ppiq_staging.accepted_batches(tenant_id,batch_id));
DO $rls$ DECLARE n text; BEGIN
 FOREACH n IN ARRAY ARRAY['accepted_source_artifacts','accepted_artifact_admissions','accepted_gaps'] LOOP
  EXECUTE format('ALTER TABLE ppiq_staging.%I ENABLE ROW LEVEL SECURITY',n);
  EXECUTE format('ALTER TABLE ppiq_staging.%I FORCE ROW LEVEL SECURITY',n);
  EXECUTE format('DROP POLICY IF EXISTS accepted_tenant ON ppiq_staging.%I',n);
  EXECUTE format('CREATE POLICY accepted_tenant ON ppiq_staging.%I USING (tenant_id=nullif(current_setting(''app.current_tenant'',true),'''')::uuid) WITH CHECK (tenant_id=nullif(current_setting(''app.current_tenant'',true),'''')::uuid)',n);
 END LOOP;
END $rls$;
CREATE OR REPLACE FUNCTION ppiq_staging.accepted_immutable() RETURNS trigger LANGUAGE plpgsql AS $f$
BEGIN RAISE EXCEPTION 'AR07 immutable_accepted_evidence' USING ERRCODE='P0001'; END $f$;
DROP TRIGGER IF EXISTS accepted_payload_immutable ON ppiq_staging.accepted_records;
CREATE TRIGGER accepted_payload_immutable BEFORE UPDATE OR DELETE ON ppiq_staging.accepted_records FOR EACH ROW EXECUTE FUNCTION ppiq_staging.accepted_immutable();
DROP TRIGGER IF EXISTS accepted_artifact_immutable ON ppiq_staging.accepted_source_artifacts;
CREATE TRIGGER accepted_artifact_immutable BEFORE UPDATE OR DELETE ON ppiq_staging.accepted_source_artifacts FOR EACH ROW EXECUTE FUNCTION ppiq_staging.accepted_immutable();
DROP TRIGGER IF EXISTS accepted_gap_immutable ON ppiq_staging.accepted_gaps;
CREATE TRIGGER accepted_gap_immutable BEFORE UPDATE OR DELETE ON ppiq_staging.accepted_gaps FOR EACH ROW EXECUTE FUNCTION ppiq_staging.accepted_immutable();

DROP TRIGGER IF EXISTS accepted_artifact_admission_immutable ON ppiq_staging.accepted_artifact_admissions;
CREATE TRIGGER accepted_artifact_admission_immutable BEFORE UPDATE OR DELETE ON ppiq_staging.accepted_artifact_admissions FOR EACH ROW EXECUTE FUNCTION ppiq_staging.accepted_immutable();

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_column_name(k text,id uuid) RETURNS text LANGUAGE sql IMMUTABLE AS $f$
 SELECT CASE WHEN k ~ '^[a-z][a-z0-9_]{0,62}$' AND k NOT IN ('source_system','source_record_id','batch_id','configuration_version','accepted_metadata')
 THEN k ELSE 'field_'||replace(id::text,'-','') END
$f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_validate_value(val jsonb,typ text,shape jsonb)
RETURNS void LANGUAGE plpgsql SET search_path=pg_catalog,ppiq_staging AS $f$
DECLARE item jsonb; n numeric; target text;
BEGIN
 IF val IS NULL THEN RAISE EXCEPTION 'AR12 missing_declared_field' USING ERRCODE='P0001'; END IF;
 IF jsonb_typeof(val)='null' THEN RETURN; END IF;
 target:=ppiq_staging.accepted_sql_type(typ);
 IF target IS NULL THEN RAISE EXCEPTION 'AR12 unsupported_type' USING ERRCODE='P0001'; END IF;
 IF shape ? 'arrayLength' THEN
  IF jsonb_typeof(val)<>'array' OR jsonb_array_length(val)<>(shape->>'arrayLength')::integer THEN
   RAISE EXCEPTION 'AR12 declared_array_shape_required' USING ERRCODE='P0001'; END IF;
  FOR item IN SELECT value FROM jsonb_array_elements(val) LOOP
   PERFORM ppiq_staging.accepted_validate_value(item,typ,shape-'arrayLength');
  END LOOP;
  RETURN;
 END IF;
 IF typ IN ('int8','uint8','int16','uint16','int32','uint32','int64','uint64','decimal','float32','float64') THEN
  IF jsonb_typeof(val)<>'number' THEN RAISE EXCEPTION 'AR12 numeric_field_requires_number' USING ERRCODE='P0001'; END IF;
  n:=(val#>>'{}')::numeric;
  IF typ IN ('int8','uint8','int16','uint16','int32','uint32','int64','uint64') AND
   (n<>trunc(n) OR n < (CASE typ WHEN 'int8' THEN -128 WHEN 'int16' THEN -32768 WHEN 'int32' THEN -2147483648 WHEN 'int64' THEN -9223372036854775808 ELSE 0 END)
   OR n > (CASE typ WHEN 'int8' THEN 127 WHEN 'uint8' THEN 255 WHEN 'int16' THEN 32767 WHEN 'uint16' THEN 65535 WHEN 'int32' THEN 2147483647 WHEN 'uint32' THEN 4294967295 WHEN 'int64' THEN 9223372036854775807 ELSE 18446744073709551615 END)) THEN
   RAISE EXCEPTION 'AR12 integer_out_of_range' USING ERRCODE='P0001'; END IF;
 ELSIF typ='boolean' THEN
  IF jsonb_typeof(val)<>'boolean' THEN RAISE EXCEPTION 'AR12 boolean_required' USING ERRCODE='P0001'; END IF;
 ELSIF jsonb_typeof(val)<>'string' THEN RAISE EXCEPTION 'AR12 string_required' USING ERRCODE='P0001'; END IF;
 IF typ='decimal' AND shape ? 'precision' AND
  (n<>round(n,(shape->>'scale')::integer) OR abs(n)>=power(10::numeric,(shape->>'precision')::integer-(shape->>'scale')::integer)) THEN
  RAISE EXCEPTION 'AR12 decimal_shape_exceeded' USING ERRCODE='P0001'; END IF;
 IF typ='string' AND length(val#>>'{}')>coalesce((shape->>'maxLength')::integer,1048576) THEN
  RAISE EXCEPTION 'AR12 string_length_exceeded' USING ERRCODE='P0001'; END IF;
 IF typ='bytes' THEN
  IF octet_length(decode(val#>>'{}','base64'))>coalesce((shape->>'maxLength')::integer,1048576) THEN
   RAISE EXCEPTION 'AR12 bytes_length_exceeded' USING ERRCODE='P0001'; END IF;
 ELSE EXECUTE format('SELECT ($1)::%s',target) USING val#>>'{}'; END IF;
END $f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_value_expression(fld jsonb)
RETURNS text LANGUAGE plpgsql IMMUTABLE SET search_path=pg_catalog,ppiq_staging AS $f$
DECLARE raw text; scalar text; target text;
BEGIN
 target:=ppiq_staging.accepted_sql_type(fld->>'type');
 IF target IS NULL THEN RAISE EXCEPTION 'AR12 unsupported_type' USING ERRCODE='P0001'; END IF;
 raw:=format('(r.envelope->''values''->%L)',fld->>'fieldId');
 scalar:=CASE WHEN fld->>'type'='bytes' THEN 'decode(x.value#>>''{}'',''base64'')' ELSE format('(x.value#>>''{}'')::%s',target) END;
 IF (fld->'typeShape') ? 'arrayLength' THEN
  RETURN format('(CASE WHEN jsonb_typeof(%s)=''null'' THEN NULL::%s[] ELSE (SELECT array_agg(%s ORDER BY x.ordinality) FROM jsonb_array_elements(%s) WITH ORDINALITY x(value,ordinality)) END)',raw,target,scalar,raw);
 END IF;
 IF fld->>'type'='bytes' THEN RETURN format('decode(%s#>>''{}'',''base64'')',raw); END IF;
 RETURN format('(%s#>>''{}'')::%s',raw,target);
END $f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_validate_metadata(e jsonb,b ppiq_staging.accepted_batches)
RETURNS void LANGUAGE plpgsql SET search_path=pg_catalog,ppiq_staging AS $f$
DECLARE required text; fld jsonb; meta jsonb; name text; art ppiq_staging.accepted_source_artifacts%ROWTYPE;
BEGIN
 IF e ? 'siteId' AND e->>'siteId' IS DISTINCT FROM b.site_id::text OR
    e ? 'configurationId' AND e->>'configurationId' IS DISTINCT FROM b.configuration_id::text OR
    e ? 'datasetGovernanceId' AND e->>'datasetGovernanceId' IS DISTINCT FROM b.dataset_governance_id::text OR
    e ? 'generation' AND (e->>'generation')::bigint IS DISTINCT FROM b.generation OR
    e ? 'tenantId' AND e->>'tenantId' IS DISTINCT FROM b.tenant_id::text OR
    e ? 'batchId' AND e->>'batchId' IS DISTINCT FROM b.batch_id::text OR
    e ? 'sessionId' AND e->>'sessionId' IS DISTINCT FROM b.session_id::text OR
    e ? 'configurationVersion' AND (e->>'configurationVersion')::integer IS DISTINCT FROM b.configuration_version THEN
  RAISE EXCEPTION 'AR11 contradictory_record_identity' USING ERRCODE='P0001'; END IF;
 SELECT g->>'requiredConsistency' INTO required FROM jsonb_array_elements(b.configuration_snapshot->'recordingGroups') g WHERE g->>'groupKey'=b.recording_group_key;
 -- Consistency classes represent different evidence, not a numeric strength hierarchy.
 IF required IS NOT NULL AND e->>'consistency' IS DISTINCT FROM required THEN
  RAISE EXCEPTION 'AR11 required_consistency_not_proven' USING ERRCODE='P0001'; END IF;
 FOREACH name IN ARRAY ARRAY['sourceTimestampUtc','serverTimestampUtc','captureStartedAtUtc','captureEndedAtUtc','edgeReceivedAtUtc','edgeDurableAtUtc','scheduledAtUtc','triggeredAtUtc'] LOOP
  IF e ? name AND jsonb_typeof(e->name)<>'null' THEN
   IF jsonb_typeof(e->name)<>'string' OR e->>name !~ '(Z|[+-][0-9]{2}:[0-9]{2})$' THEN
    RAISE EXCEPTION 'AR11 timestamp_requires_zone' USING ERRCODE='P0001'; END IF;
   PERFORM (e->>name)::timestamptz;
  END IF;
 END LOOP;
 IF e ? 'fieldMetadata' THEN
  IF jsonb_typeof(e->'fieldMetadata')<>'object' THEN RAISE EXCEPTION 'AR11 field_metadata_object_required' USING ERRCODE='P0001'; END IF;
  FOR name,meta IN SELECT key,value FROM jsonb_each(e->'fieldMetadata') LOOP
   IF NOT EXISTS(SELECT 1 FROM jsonb_array_elements(b.field_shape) f WHERE f->>'fieldId'=name) OR jsonb_typeof(meta)<>'object' THEN
    RAISE EXCEPTION 'AR11 unknown_field_metadata' USING ERRCODE='P0001'; END IF;
  END LOOP;
 END IF;
 IF e ? 'originalBytes' THEN
  SELECT * INTO art FROM ppiq_staging.accepted_source_artifacts WHERE tenant_id=b.tenant_id AND artifact_id=(e->'originalBytes'->>'artifactId')::uuid;
  IF NOT FOUND OR art.sha256 IS DISTINCT FROM e->'originalBytes'->>'sha256'
   OR octet_length(art.content) IS DISTINCT FROM (e->'originalBytes'->>'length')::integer THEN
   RAISE EXCEPTION 'AR14 immutable_artifact_not_proven' USING ERRCODE='P0001'; END IF;
 END IF;
END $f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_record_metadata(r ppiq_staging.accepted_records,b ppiq_staging.accepted_batches)
RETURNS jsonb LANGUAGE sql IMMUTABLE SET search_path=pg_catalog AS $f$
 SELECT jsonb_build_object('tenantId',r.tenant_id,'siteId',b.site_id,'datasetGovernanceId',r.dataset_governance_id,
 'streamKey',r.stream_key,'recordId',r.record_id,'batchId',r.batch_id,'sessionId',b.session_id,'generation',b.generation,
 'configurationId',b.configuration_id,'configurationVersion',b.configuration_version,'configurationVersionId',b.configuration_version_id,
 'configurationHash',b.configuration_hash,'recordingGroupKey',b.recording_group_key,'configuration',b.configuration_snapshot,
 'fields',b.field_shape,'capture',r.envelope-'values','contentHash',r.content_hash,'receiptId',r.receipt_id,
 'durablePosition',r.durable_position,'acceptedAtUtc',r.accepted_at_utc)
$f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_publish_dataset(t uuid,batch uuid)
RETURNS text LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging AS $f$
DECLARE b ppiq_staging.accepted_batches%ROWTYPE; fld jsonb; cols text:=''; rel text;
BEGIN
 PERFORM ppiq_staging.accepted_assert_tenant(t);
 SELECT * INTO b FROM ppiq_staging.accepted_batches WHERE tenant_id=t AND batch_id=batch;
 IF NOT FOUND OR b.state<>'Sealed' THEN RAISE EXCEPTION 'AR02 sealed_batch_required' USING ERRCODE='P0001'; END IF;
 rel:='accepted_dataset_'||substr(encode(public.digest(convert_to(t::text||'/'||b.dataset_governance_id::text||'/'||b.configuration_version_id::text||'/'||b.recording_group_key,'UTF8'),'sha256'),'hex'),1,40);
 FOR fld IN SELECT * FROM jsonb_array_elements(b.field_shape) LOOP
  cols:=cols||format(',%s AS %I',ppiq_staging.accepted_value_expression(fld),coalesce(fld->>'sourceColumn',fld->>'key'));
 END LOOP;
 -- One schema per exact published version and group; different shapes never coerce silently.
 EXECUTE format('CREATE OR REPLACE VIEW ppiq_staging.%I WITH(security_barrier=true) AS SELECT
 ''accepted:''||encode(public.digest(convert_to(r.tenant_id::text||''/''||r.dataset_governance_id::text||''/''||r.stream_key,''UTF8''),''sha256''),''hex'') AS source_system,
 r.record_id AS source_record_id,r.batch_id,b.configuration_version,ppiq_staging.accepted_record_metadata(r,b) AS accepted_metadata %s
 FROM ppiq_staging.accepted_records r JOIN ppiq_staging.accepted_batches b ON b.tenant_id=r.tenant_id AND b.batch_id=r.batch_id
 WHERE r.tenant_id=%L::uuid AND r.tenant_id=nullif(current_setting(''app.current_tenant'',true),'''')::uuid
 AND b.dataset_governance_id=%L::uuid AND b.configuration_version_id=%L::uuid AND b.recording_group_key=%L AND b.state=''Sealed''',rel,cols,t,b.dataset_governance_id,b.configuration_version_id,b.recording_group_key);
 IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='plantprocess_app') THEN EXECUTE format('GRANT SELECT ON ppiq_staging.%I TO plantprocess_app',rel); END IF;
 RETURN rel;
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
  'type',r.declared_type,'typeShape',r.type_shape,'unit',r.source_unit,'layoutRevision',r.layout_revision,'sourceLocator',r.source_locator,'sourceColumn',ppiq_staging.accepted_column_name(r.field_key,r.field_id)) ORDER BY r.field_key),count(*)
 INTO shape,key_count FROM jsonb_array_elements(doc->'fields') x
 JOIN ppiq_meta.source_field_revisions r ON r.field_id=(x->>'fieldId')::uuid AND r.revision=(x->>'revision')::integer AND r.tenant_id=p_tenant
 JOIN ppiq_meta.source_field_identities i ON i.field_id=r.field_id AND i.tenant_id=p_tenant AND i.dataset_governance_id=p_dataset
 WHERE (grp->'memberFieldIds') ? (r.field_id::text);
 IF shape IS NULL OR key_count > 1595 OR key_count <> jsonb_array_length(grp->'memberFieldIds') THEN
  RAISE EXCEPTION 'AR12 governed_field_shape_required' USING ERRCODE='P0001'; END IF;
 FOR fld IN SELECT * FROM jsonb_array_elements(shape) LOOP
  IF ppiq_staging.accepted_sql_type(fld->>'type') IS NULL THEN
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
 UPDATE ppiq_staging.accepted_batches SET site_id=(SELECT site_id FROM ppiq_staging.accepted_capture_fences WHERE tenant_id=p_tenant AND dataset_governance_id=p_dataset AND stream_key=p_stream),configuration_snapshot=doc, configuration_hash=(SELECT definition_hash FROM ppiq_meta.definition_versions WHERE id=v) WHERE tenant_id=p_tenant AND batch_id=p_batch;
 RETURN p_batch;
END $f$;

CREATE OR REPLACE FUNCTION ppiq_staging.accepted_append(p_tenant uuid,p_batch uuid,p_session uuid,
 p_generation bigint,p_record text,p_envelope jsonb)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging,ppiq_meta AS $f$
DECLARE b ppiq_staging.accepted_batches%ROWTYPE; r ppiq_staging.accepted_records%ROWTYPE;
 h text; proof uuid; bytes bigint; fld jsonb; val jsonb; typ text; n numeric; k text;
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
 PERFORM ppiq_staging.accepted_validate_metadata(p_envelope,b);
 IF b.state <> 'Open' THEN RAISE EXCEPTION 'AR05 batch_is_sealed' USING ERRCODE='P0001'; END IF;
 -- Pin the exact producer-issued admission. Envelope assertions cannot authorize preservation.
 SELECT a.proof_id INTO proof FROM ppiq_staging.accepted_artifact_admissions a
 WHERE a.tenant_id=p_tenant AND a.batch_id=p_batch AND a.record_id=p_record
 AND a.artifact_id=(p_envelope->'originalBytes'->>'artifactId')::uuid
 AND a.policy_reference=b.configuration_snapshot->'storage'->>'retentionPolicyRef'
 AND a.retention_until_utc>clock_timestamp() ORDER BY a.retention_until_utc DESC,a.proof_id LIMIT 1;
 IF b.preservation_required AND proof IS NULL THEN
 RAISE EXCEPTION 'AR14 preservation_authority_unavailable' USING ERRCODE='P0001'; END IF;
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
  PERFORM ppiq_staging.accepted_validate_value(val,typ,fld->'typeShape');
 END LOOP;
 bytes:=octet_length(convert_to(p_envelope::text,'UTF8'));
 IF b.record_count>=b.maximum_records OR b.payload_bytes+bytes>b.maximum_bytes THEN
  RAISE EXCEPTION 'AR11 finite_batch_capacity_exceeded' USING ERRCODE='P0001'; END IF;
 INSERT INTO ppiq_staging.accepted_records(tenant_id,dataset_governance_id,stream_key,record_id,batch_id,envelope,content_hash,payload_bytes,original_bytes_proof_id)
 VALUES(p_tenant,b.dataset_governance_id,b.stream_key,p_record,p_batch,p_envelope,h,bytes,proof) RETURNING * INTO r;
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
 IF EXISTS(SELECT 1 FROM ppiq_staging.accepted_gaps WHERE tenant_id=p_tenant AND batch_id=p_batch) THEN
  RAISE EXCEPTION 'AR16 gap_bearing_batch_requires_runtime_disposition' USING ERRCODE='P0001'; END IF;
 FOR fld IN SELECT * FROM jsonb_array_elements(b.field_shape) LOOP
  expr:=ppiq_staging.accepted_value_expression(fld);
  cols:=cols || format(',%s AS %I',expr,coalesce(fld->>'sourceColumn',fld->>'key'));
 END LOOP;
 rel:='accepted_batch_'||replace(p_batch::text,'-','');
 -- Exact immutable batch relation. No CREATE OR REPLACE: name collision is drift.
 EXECUTE format('CREATE VIEW ppiq_staging.%I WITH (security_barrier=true) AS SELECT
  %L::text AS source_system,r.record_id::text AS source_record_id,r.batch_id,%s::integer AS configuration_version,ppiq_staging.accepted_record_metadata(r,b) AS accepted_metadata %s
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
 PERFORM ppiq_staging.accepted_publish_dataset(p_tenant,p_batch);
 RETURN p_batch;
END $f$;


CREATE OR REPLACE FUNCTION ppiq_staging.accepted_put_artifact(t uuid,id uuid,data bytea,media text,expected text)
RETURNS text LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging AS $f$
DECLARE h text; existing ppiq_staging.accepted_source_artifacts%ROWTYPE;
BEGIN
 PERFORM ppiq_staging.accepted_assert_tenant(t);
 h:=encode(public.digest(data,'sha256'),'hex');
 IF h IS DISTINCT FROM lower(expected) OR id IS NULL OR octet_length(data) NOT BETWEEN 1 AND 16777216 THEN
  RAISE EXCEPTION 'AR14 artifact_integrity_failed' USING ERRCODE='P0001'; END IF;
 INSERT INTO ppiq_staging.accepted_source_artifacts(tenant_id,artifact_id,content,sha256,media_type)
 VALUES(t,id,data,h,media) ON CONFLICT DO NOTHING;
 SELECT * INTO existing FROM ppiq_staging.accepted_source_artifacts WHERE tenant_id=t AND artifact_id=id;
 IF existing.content IS DISTINCT FROM data OR existing.media_type IS DISTINCT FROM media THEN
  RAISE EXCEPTION 'AR07 artifact_identity_conflict' USING ERRCODE='P0001'; END IF;
 RETURN h;
END $f$;
CREATE OR REPLACE FUNCTION ppiq_staging.accepted_record_gap(t uuid,batch uuid,s uuid,g bigint,id uuid,why text,lo text,hi text,n bigint)
RETURNS uuid LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging AS $f$
DECLARE b ppiq_staging.accepted_batches%ROWTYPE; old ppiq_staging.accepted_gaps%ROWTYPE;
BEGIN
 PERFORM ppiq_staging.accepted_assert_tenant(t);
 SELECT * INTO b FROM ppiq_staging.accepted_batches WHERE tenant_id=t AND batch_id=batch;
 IF NOT FOUND THEN RAISE EXCEPTION 'AR02 batch_unknown' USING ERRCODE='P0001'; END IF;
 PERFORM ppiq_staging.accepted_lock_fence(t,b.dataset_governance_id,b.stream_key,s,g);
 SELECT * INTO b FROM ppiq_staging.accepted_batches WHERE tenant_id=t AND batch_id=batch FOR UPDATE;
 IF ROW(b.session_id,b.generation) IS DISTINCT FROM ROW(s,g) THEN RAISE EXCEPTION 'AR04 stale_batch_owner' USING ERRCODE='P0001'; END IF;
 SELECT * INTO old FROM ppiq_staging.accepted_gaps WHERE tenant_id=t AND gap_id=id;
 IF FOUND THEN
  IF ROW(old.batch_id,old.reason,old.from_position,old.to_position,old.missing_count) IS DISTINCT FROM ROW(batch,why,lo,hi,n) THEN
   RAISE EXCEPTION 'AR07 gap_identity_conflict' USING ERRCODE='P0001'; END IF;
  RETURN id;
 END IF;
 IF b.state<>'Open' THEN RAISE EXCEPTION 'AR05 batch_is_sealed' USING ERRCODE='P0001'; END IF;
 INSERT INTO ppiq_staging.accepted_gaps(tenant_id,batch_id,gap_id,reason,from_position,to_position,missing_count) VALUES(t,batch,id,why,lo,hi,n);
 RETURN id;
END $f$;
-- Exact envelope is composed from the one payload plus frozen metadata, never copied.
CREATE OR REPLACE FUNCTION ppiq_staging.accepted_has_receipt(t uuid,batch uuid,record text)
RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging AS $f$
BEGIN
 PERFORM ppiq_staging.accepted_assert_tenant(t);
 RETURN EXISTS(SELECT 1 FROM ppiq_staging.accepted_records r JOIN ppiq_staging.accepted_batches b
 ON b.tenant_id=r.tenant_id AND b.dataset_governance_id=r.dataset_governance_id AND b.stream_key=r.stream_key
 WHERE r.tenant_id=t AND b.batch_id=batch AND r.record_id=record);
END $f$;
CREATE OR REPLACE FUNCTION ppiq_staging.accepted_record_detail(t uuid,receipt uuid)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,ppiq_staging AS $f$
DECLARE result jsonb;
BEGIN
 PERFORM ppiq_staging.accepted_assert_tenant(t);
 SELECT jsonb_build_object('receiptId',r.receipt_id,'recordId',r.record_id,'tenantId',r.tenant_id,
 'siteId',b.site_id,'sourceDatasetId',(SELECT source_dataset_definition_id FROM ppiq_meta.source_dataset_governance WHERE tenant_id=t AND id=b.dataset_governance_id),
 'connectionProfileId',(SELECT connection_profile_id FROM ppiq_meta.source_dataset_governance WHERE tenant_id=t AND id=b.dataset_governance_id),
 'datasetGovernanceId',r.dataset_governance_id,'streamKey',r.stream_key,'sessionId',b.session_id,'generation',b.generation,
 'batchId',b.batch_id,'configurationId',b.configuration_id,'configurationVersion',b.configuration_version,
 'configurationVersionId',b.configuration_version_id,'configurationHash',b.configuration_hash,'configuration',b.configuration_snapshot,
 'recordingGroupKey',b.recording_group_key,'fields',b.field_shape,'acceptedAtUtc',r.accepted_at_utc,
 'durablePosition',r.durable_position,'contentHash',r.content_hash,'envelope',r.envelope,
 'originalBytesAdmission',(SELECT to_jsonb(a)-'tenant_id' FROM ppiq_staging.accepted_artifact_admissions a WHERE a.tenant_id=r.tenant_id AND a.proof_id=r.original_bytes_proof_id)) INTO result
 FROM ppiq_staging.accepted_records r JOIN ppiq_staging.accepted_batches b ON b.tenant_id=r.tenant_id AND b.batch_id=r.batch_id
 WHERE r.tenant_id=t AND r.receipt_id=receipt AND b.state='Sealed';
 RETURN result;
END $f$;
REVOKE ALL ON ppiq_staging.accepted_source_artifacts,ppiq_staging.accepted_artifact_admissions,ppiq_staging.accepted_gaps FROM PUBLIC;
REVOKE ALL ON FUNCTION ppiq_staging.accepted_publish_dataset(uuid,uuid),ppiq_staging.accepted_put_artifact(uuid,uuid,bytea,text,text),ppiq_staging.accepted_record_gap(uuid,uuid,uuid,bigint,uuid,text,text,text,bigint),ppiq_staging.accepted_record_detail(uuid,uuid),ppiq_staging.accepted_has_receipt(uuid,uuid,text) FROM PUBLIC;
DO $grants$ BEGIN
 IF EXISTS(SELECT 1 FROM pg_roles WHERE rolname='plantprocess_app') THEN
  REVOKE ALL ON ppiq_staging.accepted_source_artifacts,ppiq_staging.accepted_artifact_admissions,ppiq_staging.accepted_gaps FROM plantprocess_app;
  GRANT SELECT ON ppiq_staging.accepted_gaps TO plantprocess_app;
  GRANT EXECUTE ON FUNCTION ppiq_staging.accepted_put_artifact(uuid,uuid,bytea,text,text),ppiq_staging.accepted_record_gap(uuid,uuid,uuid,bigint,uuid,text,text,text,bigint),ppiq_staging.accepted_record_detail(uuid,uuid),ppiq_staging.accepted_has_receipt(uuid,uuid,text) TO plantprocess_app;
 END IF;
END $grants$;
DO $existing$
DECLARE b record; tenant uuid; previous text:=current_setting('app.current_tenant',true);
BEGIN
 FOR tenant IN SELECT id FROM ppiq_meta.tenants LOOP
  PERFORM set_config('app.current_tenant',tenant::text,true);
  UPDATE ppiq_staging.accepted_batches ab SET configuration_snapshot=v.graph_json,configuration_hash=v.definition_hash
  FROM ppiq_meta.definition_versions v WHERE ab.tenant_id=tenant AND v.id=ab.configuration_version_id AND ab.configuration_snapshot IS NULL;
  FOR b IN SELECT tenant_id,batch_id FROM ppiq_staging.accepted_batches WHERE tenant_id=tenant AND state='Sealed' LOOP
   PERFORM ppiq_staging.accepted_publish_dataset(b.tenant_id,b.batch_id);
  END LOOP;
 END LOOP;
 PERFORM set_config('app.current_tenant',coalesce(previous,''),true);
END $existing$;
COMMIT;
