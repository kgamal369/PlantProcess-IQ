# PlantProcess IQ - Master Design Document

**Version 4.0 | Author: Karim, SOU Industrial Software, Dusseldorf**

*File maps to PPIQ.txt sections 4.5 and 4.6, labelled "Chapter 3: General software product technical Function Description".*

---

# SECTION 4, PART 3 - SCHEMA, KEYS, JOINS, TOPOLOGY AND CREDENTIALS

> **Audience (4.7):** the customer's database administrator and security reviewer, and our developers taking hand-over.
>
> **Voice (4.8):** senior software engineer and technical lead.
>
> **Completes:** 4.5 (every schema, table, primary key, foreign key, index, constraint and join path) and 4.6 (credentials and topology in full).

---

## 4.5 Database schemas, tables, keys and joins

### 4.5.1 Universal conventions

Applied to every table unless the table's own entry overrides it.

| Column | Type | Rule |
|---|---|---|
| `id` | `uuid` | **PRIMARY KEY**, `DEFAULT gen_random_uuid()` |
| `tenant_id` | `uuid` | `NOT NULL`. Row-level security predicate column (4.5.8) |
| `created_at_utc` | `timestamptz` | `NOT NULL` |
| `updated_at_utc` | `timestamptz` | `NULL` |
| `is_deleted` | `boolean` | `NOT NULL DEFAULT false` |
| `deleted_at_utc` | `timestamptz` | `NULL` |
| `deleted_reason` | `varchar(500)` | `NULL` |
| `is_synthetic` | `boolean` | `NOT NULL DEFAULT false`. Separates emulated from production data |

**The provenance triple**, on every table that holds imported data:

| Column | Type |
|---|---|
| `source_system` | `varchar(100)` |
| `source_record_id` | `varchar(100)` |
| `import_batch_id` | `uuid` |

**Four standing rules.**

1. **Surrogate keys are internal; business keys are the customer's.** Every join in analysis resolves to a `uuid`, never to a source string. Business keys carry a unique index so projection is idempotent, but they are never the join target.
2. **Soft delete, never hard delete, on any row an audit entry may reference.** Cascading deletes never cross a provenance boundary.
3. **Re-projection supersedes; it does not destroy.** A second run over the same batch updates in place under the filtered unique index of 4.5.3.
4. **Time is stored twice where a shift matters:** universal, local, and the zone identifier. A single universal column silently corrupts shift attribution twice a year at the daylight-saving boundary.

### 4.5.2 Schema topology

| Schema | Holds | Day one | Written by |
|---|---|---|---|
| `ppiq_staging` | Source-shaped copies, envelopes, watermarks | Empty | The import pipeline only |
| `ppiq_plant` | The canonical model and the analytical results area | **Empty, provably, one query** | The projector and the engines only |
| `ppiq_meta` | Product configuration | Prefilled under a declared per-table contract, from versioned scripts, each row past the genericity lint | The application |
| `public` | Platform infrastructure only: extensions, migration history | - | The platform |

**The classification test, one question: whose knowledge is this row?** The customer's plant reality goes to `ppiq_plant`. The product's configuration goes to `ppiq_meta`. An uninterpreted copy of what a source sent goes to `ppiq_staging`. A row that seems to belong to two is two tables.

**Isolation as topology.** No surface reads `ppiq_staging`. Administration surfaces read `ppiq_meta` and never plant rows. Analytical surfaces read `ppiq_plant` only.

**Transitional note.** The governing data-flow contract operates staging under the physical name `dump_store`, with the schema name held in one configuration key (`Prep:StagingSchema`) precisely so the rename to `ppiq_staging` is one configuration-and-migration change. Emulator source shapes live in `src_*_shape` schemas which belong to the emulated factory, not to the product. This section specifies the target; the Implementation Status Register carries the distance and the migration script.

### 4.5.3 `ppiq_staging` - transit

**`import_batches`** - one row per push; the unit of lineage and retry.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | PK |
| `source_dataset_definition_id` | uuid | FK -> `ppiq_meta.source_dataset_definitions(id)` ON DELETE RESTRICT |
| `source_object_name` | varchar(200) | NOT NULL |
| `source_system` | varchar(100) | NOT NULL |
| `status` | varchar(50) | NOT NULL. `Pending | Running | Completed | Failed | Cancelled` |
| `started_at_utc`, `finished_at_utc` | timestamptz | |
| `row_count` | integer | NOT NULL DEFAULT 0 |
| `watermark_from`, `watermark_to` | text | The cursor range actually read |
| `checksum` | varchar(128) | Of the read payload |
| `failure_reason` | varchar(4000) | NULL. **A failure is stored, not only logged** |

Indexes: `(source_dataset_definition_id, started_at_utc DESC)`; `(status)`; `(started_at_utc DESC)`.

**`staging_records`** - one row per source row, verbatim.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | PK |
| `import_batch_id` | uuid | **FK -> `import_batches(id)` ON DELETE RESTRICT** |
| `source_object_name` | varchar(200) | NOT NULL |
| `row_number` | integer | NOT NULL |
| `raw_json` | **jsonb** | NOT NULL. The source row, uninterpreted |
| `is_processed` | boolean | NOT NULL DEFAULT false |
| `processed_at_utc` | timestamptz | NULL |
| `processing_status` | varchar(50) | NOT NULL DEFAULT `'Pending'` |
| `processing_error` | varchar(4000) | NULL |
| `canonical_entity_id` | uuid | NULL. What this row became |
| `canonical_entity_name` | varchar(200) | NULL |

Indexes: `(import_batch_id, row_number)` unique; `(processing_status)` partial `WHERE is_processed = false`; GIN on `raw_json`.

*`ON DELETE RESTRICT` is deliberate: a batch cannot be deleted while its rows exist, because that orphans the lineage every canonical row depends on. The partial index exists because the projector's hot query is "unprocessed rows in this batch", and a partial index on that predicate stays small no matter how large staging grows.*

**`cursor_watermarks`**: `source_dataset_definition_id` (FK, unique), `watermark_column`, `watermark_value text`, `watermark_type`, `last_advanced_at_utc`. One row per dataset; the freshness ground truth the gate reads.

**`schema_drift_events`**: `source_dataset_definition_id` (FK), `detected_at_utc`, `change_type` (`ColumnAdded | ColumnRemoved | TypeChanged | ObjectMissing`), `column_name`, `old_type`, `new_type`, `acknowledged_at_utc`, `acknowledged_by`. Index `(source_dataset_definition_id, detected_at_utc DESC)`.

**`edge_collector_batches`** and **`edge_collector_buffer_status`**: the collector's own queue and buffer state, so a one-way push is resumable across a network outage.

### 4.5.4 `ppiq_plant` - the canonical model

#### Cluster 1: plant structure

**`sites`**: `site_code varchar(50) NOT NULL`, `site_name varchar(200)`, `plant_time_zone_id varchar(100) NOT NULL`, `country varchar(100)`. Unique `(site_code)`.

**`areas`**: `site_id uuid NOT NULL FK -> sites(id)`, `area_code varchar(50) NOT NULL`, `area_name varchar(200)`. Unique `(site_id, area_code)`; index `(site_id)`.

**`equipment`**: `area_id uuid NOT NULL FK -> areas(id)`, `equipment_code varchar(50) NOT NULL`, `equipment_name varchar(200)`, `equipment_type varchar(100)`, provenance triple. Unique `(area_id, equipment_code)`; indexes `(area_id)`, `(equipment_type)`.

**`routes`**: `site_id uuid FK -> sites(id)`, `route_code varchar(50) NOT NULL`, `route_name varchar(200)`. Unique `(site_id, route_code)`.

**`route_steps`**: `route_id uuid NOT NULL FK -> routes(id) ON DELETE CASCADE`, `operation_definition_id uuid FK -> operation_definitions(id)`, `equipment_id uuid FK -> equipment(id)`, `step_order integer NOT NULL`. Unique `(route_id, step_order)`; indexes `(route_id)`, `(equipment_id)`.

**`operation_definitions`**: `operation_code varchar(50) NOT NULL`, `operation_name varchar(200)`, `operation_type varchar(100)`. Unique `(operation_code)`.

**`material_unit_type_definitions`**: `unit_type_code varchar(50) NOT NULL`, `unit_type_name varchar(200)`, `grain_level integer NOT NULL`, `parent_unit_type_code varchar(50)`. Unique `(unit_type_code)`; index `(grain_level)`. **Imported vocabulary; the multi-grain hierarchy is declared here, never shipped.**

**`industry_templates`**: `template_code`, `template_name`, `payload jsonb`. Configuration only; it can never seed plant data.

#### Cluster 2: material and genealogy - the heart

**`material_units`**

| Column | Type |
|---|---|
| `material_code` | varchar(100) NOT NULL |
| `material_unit_type` | varchar(50) NOT NULL |
| `product_family` | varchar(100) |
| `grade_or_recipe` | varchar(100) |
| `site_id` | uuid FK -> `sites(id)` |
| `native_grain` | varchar(50) |
| `production_start_utc`, `production_end_utc` | timestamptz |
| provenance triple, audit, soft-delete | per conventions |

Indexes:

| Index | Purpose |
|---|---|
| **unique `(site_id, material_code)`** | The business key. One code per site |
| **unique `(source_system, source_record_id)` FILTERED** `WHERE source_system IS NOT NULL AND source_record_id IS NOT NULL` | Makes projection idempotent per source row **without forbidding rows that legitimately have no source identity.** This is the mechanism behind safe re-execution |
| `(site_id)` | Site-scoped scans |
| `(material_unit_type)` | Grain-scoped scans |
| `(site_id, material_unit_type)` | The common workspace filter pair |
| `(material_unit_type, grade_or_recipe)` | The common analysis stratification pair |

**`material_aliases`**: `material_unit_id uuid NOT NULL FK -> material_units(id) ON DELETE CASCADE`, `alias_system varchar(100) NOT NULL`, `alias_value varchar(100) NOT NULL`, provenance triple. Unique `(alias_system, alias_value)`; index `(material_unit_id)`.

*This table is what makes cross-source identity resolution auditable. The customer's several identifiers for one physical unit resolve here; analysis then joins on `material_unit_id` only.*

**`genealogy_edges`**

| Column | Type |
|---|---|
| `parent_material_unit_id` | uuid NOT NULL FK -> `material_units(id)` |
| `child_material_unit_id` | uuid NOT NULL FK -> `material_units(id)` |
| `relationship_type` | varchar(50) NOT NULL |
| `contribution_weight` | **numeric(9,6) NOT NULL** |
| `provenance_confidence` | numeric(9,6) NOT NULL |
| `is_transition` | boolean NOT NULL DEFAULT false |
| `effective_from_utc`, `effective_to_utc` | timestamptz |
| provenance triple | |

Indexes: `(parent_material_unit_id)`; `(child_material_unit_id)`; **unique `(parent_material_unit_id, child_material_unit_id)`**; and the covering index **`(child_material_unit_id, is_transition, contribution_weight)`**.

**Constraints.** `CHECK (contribution_weight > 0 AND contribution_weight <= 1)`; `CHECK (parent_material_unit_id <> child_material_unit_id)`; and **the invariant `SUM(contribution_weight) = 1.0` exactly per child**, enforced by a deferred constraint trigger on insert, update and delete.

*Three design points. `numeric(9,6)` rather than a float, because a float cannot hold that invariant. `is_transition` marks a unit spanning two parents, which is the only case where blended attribution actually matters. And the covering index exists because the feature loader's hot query reads exactly those three columns per child, so it never touches the heap.*

#### Cluster 3: process execution

**`process_step_executions`**: `material_unit_id uuid NOT NULL FK -> material_units(id)`, `route_step_id uuid FK -> route_steps(id)`, `equipment_id uuid FK -> equipment(id)`, `started_at_utc`, `ended_at_utc`, `started_at_local timestamp`, `plant_time_zone_id varchar(100) NOT NULL`, `duration_seconds integer`, `status varchar(50)`, provenance triple. Indexes `(material_unit_id)`, `(route_step_id)`, `(equipment_id)`, `(started_at_utc)`, `(material_unit_id, started_at_utc)`.

**`process_events`**: `equipment_id uuid FK -> equipment(id)`, `material_unit_id uuid NULL FK -> material_units(id)`, `event_type varchar(100) NOT NULL`, `event_at_utc`, `event_at_local`, `plant_time_zone_id`, `payload jsonb`, provenance triple. Indexes `(equipment_id, event_at_utc)`, `(material_unit_id)`, `(event_type)`.

**`parameter_definitions`**: `parameter_code varchar(100) NOT NULL`, `parameter_name varchar(200)`, `unit_of_measure varchar(50)`, `data_type varchar(50)`, `equipment_id uuid NULL FK`, `operation_definition_id uuid NULL FK`, `min_expected numeric(18,6)`, `max_expected numeric(18,6)`, provenance triple. Unique `(parameter_code)`; index `(equipment_id)`. **Imported vocabulary.** `min_expected` and `max_expected` are what a chemistry-range rule reads in S5.

**`parameter_observations`** - the volume table.

| Column | Type |
|---|---|
| `material_unit_id` | uuid FK -> `material_units(id)` |
| `parameter_definition_id` | uuid FK -> `parameter_definitions(id)` |
| `process_step_execution_id` | uuid NULL FK -> `process_step_executions(id)` |
| `equipment_id` | uuid NULL FK -> `equipment(id)` |
| `observed_at_utc` | timestamptz |
| `observed_at_local` | **timestamp without time zone** |
| `plant_time_zone_id` | varchar(100) NOT NULL |
| `numeric_value` | **numeric(18,6)** |
| `text_value` | varchar(500) |
| `unit_of_measure` | varchar(50) |
| `quality_flag` | varchar(50) NOT NULL |
| `raw_value` | varchar(500) |
| provenance triple | |

Indexes: `(material_unit_id)`, `(parameter_definition_id)`, `(process_step_execution_id)`, `(equipment_id)`, `(observed_at_utc)`, `(observed_at_local)`, and the composite `(parameter_definition_id, observed_at_utc)` for a single-parameter time series.

**Partitioning.** Range-partitioned by `observed_at_utc`, monthly, from the Medium capacity class upward. Retention and downsampling per Chapter 7. *`raw_value` is retained deliberately: the original string survives, so a parsing dispute is resolvable rather than arguable.*

#### Cluster 4: quality and loss

**`defect_catalogs`**: `defect_code varchar(100) NOT NULL`, `defect_name varchar(200)`, `defect_category varchar(100)`, `severity_default varchar(50)`, provenance triple. Unique `(source_system, defect_code)`; index `(defect_category)`. **Imported taxonomy, per source, never seeded.**

**`quality_events`**

| Column | Type |
|---|---|
| `material_unit_id` | uuid FK -> `material_units(id)` |
| `defect_catalog_id` | uuid NULL FK -> `defect_catalogs(id)` |
| `event_type` | varchar(100) NOT NULL |
| `severity` | varchar(50) |
| `decision` | varchar(100) |
| `description` | varchar(1000) |
| `event_at_utc` | timestamptz |
| `event_at_local` | timestamp without time zone |
| `plant_time_zone_id` | varchar(100) NOT NULL |
| `position_json` | jsonb |
| provenance triple | |

Indexes: `(material_unit_id)`, `(defect_catalog_id)`, `(event_type)`, `(event_at_utc)`, `(event_at_local)`, `(material_unit_id, event_type, event_at_utc)`.

**Acceptance query for the taxonomy rule:** `SELECT count(*) FROM quality_events WHERE defect_catalog_id IS NULL` must not grow after a projection. A growing count means the resolver did not find the imported catalogue, which means taxonomy was not imported first.

**`downtime_events`**: `equipment_id uuid NOT NULL FK -> equipment(id)`, `started_at_utc`, `ended_at_utc`, **`stopped_minutes numeric(12,3) NOT NULL`**, **`production_impact_minutes numeric(12,3) NOT NULL`**, `cause_code varchar(100)`, `cause_description varchar(1000)`, provenance triple. Indexes `(equipment_id, started_at_utc)`, `(cause_code)`.

*Two quantities, always both, never interchanged (Chapter 1.7). A twenty-minute mill stoppage absorbed by buffering is twenty stopped minutes and zero impact minutes. A three-minute caster pump stoppage forcing a sequence rebuild is three stopped minutes and several hundred impact minutes. Storing one column makes every value calculation wrong.*

**`data_quality_issues`**: `issue_class varchar(100) NOT NULL`, `severity varchar(50) NOT NULL`, `entity_name varchar(200)`, `entity_id uuid NULL`, `source_dataset_definition_id uuid NULL FK`, `first_seen_at_utc`, `last_seen_at_utc`, `occurrence_count integer`, `detail jsonb`. Indexes `(issue_class, severity)`, `(source_dataset_definition_id)`.

#### Cluster 5: the results area (also `ppiq_plant`, because it derives from customer data)

**`compute_runs`**: `analysis_definition_id uuid FK`, `run_status varchar(50) NOT NULL` (`Running | Completed | Blocked | Failed | Reaped`), `outcome_code`, `grain_code`, `window_days integer`, `started_at_utc`, `finished_at_utc`, **`gate_state varchar(20)`**, **`gate_evidence text`**, `blocking_dimension varchar(100)`, `engine_kind varchar(20)`, `definition_version integer`. Indexes `(analysis_definition_id, started_at_utc DESC)`, `(run_status)` partial `WHERE run_status = 'Running'`.

*The partial index on running is what the reaper scans. `gate_evidence` is the string that makes a blocked run explainable from the database alone.*

**`correlation_results`**: `compute_run_id uuid NOT NULL FK -> compute_runs(id) ON DELETE CASCADE`, `feature_code`, `outcome_code`, `method varchar(50) NOT NULL`, `effect_size numeric(18,6)`, `p_value numeric(18,12)`, `q_value numeric(18,12)`, `sample_size integer`, `odds_ratio numeric(18,6)`, `population_description text`, `stability_lower numeric`, `stability_upper numeric`, `sign_consistency numeric`, `is_stable boolean`, `stratum_survival jsonb`, **`framing_text text NOT NULL`**, **`llm_participated boolean NOT NULL DEFAULT false`**. Indexes `(compute_run_id)`, `(outcome_code, q_value)`, `(feature_code)`.

*`framing_text` and `llm_participated` are stored as data, not rendered as interface copy, so the framing survives an export, a screenshot and a report (Chapter 1.5.5).*

**`risk_scores`**: `material_unit_id uuid FK`, `model_registry_id uuid FK`, `score numeric(9,6)`, `risk_class varchar(50)`, `horizon_hours integer`, `drivers jsonb`, `scored_at_utc`. Indexes `(material_unit_id, scored_at_utc DESC)`, `(risk_class)`.

**`model_registry`**: `model_code`, `model_version integer`, `algorithm varchar(100)`, `feature_list jsonb`, `training_window_from/to`, `metrics jsonb`, `status varchar(50)`, `trained_at_utc`. Unique `(model_code, model_version)`.

**`value_impacts`**: `finding_id uuid FK -> correlation_results(id)`, `period_from/to`, `lower_bound numeric(18,2)`, `upper_bound numeric(18,2)`, `point_estimate numeric(18,2)`, `currency char(3) NOT NULL DEFAULT 'EUR'`, `inputs jsonb NOT NULL`, **`basis_status varchar(30) NOT NULL`** (`Sufficient | InsufficientBasis`), `computed_at_utc`. Index `(finding_id)`.

**`cost_assumptions`** and **`cost_assumption_audit`**: per-tenant euro-per-tonne, cost-per-impact-minute, grade premium, each with effective dates; every change audited. **A missing assumption produces `InsufficientBasis`, never a default the vendor invented.**

**`value_realization_ledger`**: `suggestion_id`, `decided_at_utc`, `decision`, `expected_lower/upper`, `observed_value`, `observed_at_utc`. This is what makes the pilot's measurement real rather than asserted.

**`suggestions`**, **`suggestion_audit`**, **`suggestion_comments`**: the recommendation layer with evidence references and outcome tracking.

**`assistant_chunks`**: `chunk_family varchar(50) NOT NULL` (`CONNECTOR | DATASET | MAPPING | DOC | FINDING`), `source_entity_name`, `source_entity_id`, `content text`, `embedding vector`, `role_scope varchar(50) NOT NULL`, `indexed_at_utc`. Indexes `(chunk_family)`, `(role_scope)`, and a vector index on `embedding`. **`role_scope` is why retrieval cannot leak across roles.**

**`assistant_audit_log`**: `asked_at_utc`, `actor_id`, `question text`, `answer text`, `citations jsonb`, `tools_invoked jsonb`, `egress_plan jsonb`, `refused boolean`, `refusal_reason`. *The assistant writes nothing but this.*

**`plant_data_log`** and **`alert_rules`**: specified in 4.5.6, because their evaluation is operational rather than analytical.

### 4.5.5 `ppiq_meta` - product configuration

Each table declares its prefill state, per Rule 2.

| Table | Key columns | Prefill |
|---|---|---|
| `tenants` | `tenant_code` unique | **Prefilled**: one row at install |
| `users` | `username` unique, `password_hash`, `is_bootstrap_admin`, `mfa_required` | **Prefilled**: vendor support only |
| `roles` | `role_code` unique | **Prefilled**: the eight-role catalogue |
| `role_permissions` | `(role_id, surface_code, action_code)` unique | **Prefilled**: the default matrix |
| `user_permission_overrides` | `(user_id, surface_code, action_code)` unique | Empty |
| `sessions` | `refresh_token_hash`, `expires_at_utc` | Empty |
| `license_artifacts` | `token_blob`, `signature`, `tier_code`, `issued_at`, `expires_at` | **Prefilled**: the install token |
| `authoring_quotas` | `(scope_type, scope_id, object_type)` unique | **Prefilled**: role defaults per tier |
| `connection_profiles` | `code` unique, `provider_type`, `vault_reference`, `source_system_tag`, budget columns | Empty |
| `source_system_definitions` | `system_code` unique | Empty |
| `source_dataset_definitions` | `(connection_profile_id, source_schema, source_table)` unique | Empty |
| `source_field_definitions` | `(source_dataset_definition_id, column_name)` unique | Empty |
| `mapping_definitions` | `mapping_code`, `mapping_version`; unique `(mapping_code, mapping_version)`; `lifecycle_status`, `definition_hash`, `output_schema jsonb`, `rollback_pointer` | Empty |
| `schema_view_definitions` | `view_code` unique, `sql_text`, `approval_status` | Empty |
| `job_definitions` | `job_code` unique, `job_class`, `schedule_expression`, `pool_code`, **`compute_weight`**, `is_enabled` | **Prefilled**: the Supervisor weekly definition only |
| `job_run_history` | `(job_definition_id, started_at_utc)` | Empty |
| `dashboard_definitions` | `dashboard_code` unique, `layout_json`, `audience_roles` | **Prefilled only if genericity-lint clean** |
| `dashboard_widget_definitions` | `(dashboard_definition_id, widget_code)` unique; `widget_kind`, `chart_type`, `dimension_code`, `measure_code`, `expression_text`, `filter_json` | As above |
| `widget_expression_status` | `(widget_definition_id)` unique; `declared`, `served`, `last_checked_at_utc` | Empty |
| `kpi_definitions` | `kpi_code` unique | Empty |
| `registry_dimensions`, `registry_measures` | `code` unique; `source_table`, `source_column`, `data_type`, `is_filterable` | **Derived, not prefilled** - see the note below |
| `chart_type_registry` | `chart_type_code` unique; `supports_dimension`, `supports_measure`, `requires_dimension` | **Prefilled**: product grammar |
| `log_channels` | `channel_code` unique; `severity_map jsonb`, `retention_days`, `export_target`, `reading_roles`, `is_builtin` | **Prefilled**: the four built-ins, locked |
| `audit_log_entries` | `(occurred_at_utc, actor_id)`; **append-only** | Grows from install |
| `translations` | `(label_key, language_code)` unique; `text`, `review_state` | **Prefilled**: shipped languages |
| `system_settings` | `setting_key` unique | **Prefilled**: documented defaults |

**The registry note, and it is the most consequential design item in this section.** `registry_dimensions` and `registry_measures` are **derived from the canonical model and the customer's own mapping**, not shipped as rows and not compiled into a code-level set. The present implementation holds them as a compiled `HashSet` containing plant vocabulary - `ShiftCode`, `DefectType`, `RiskClass`, `GradeOrRecipe` - which is a Rule 1 violation even though every value is dynamic, because a plant that filters by batch, recipe, tool number or ambient humidity cannot add a dimension. **What stays closed is the chart-type registry, the widget-kind set and the numeric safety limits**, because those are the product's own grammar and its safety envelope.

**Safety limits, held as configuration and enforced server-side:** default maximum returned rows 100, absolute 500; default raw row limit 50,000, absolute 250,000; default lookback 90 days, absolute 730.

### 4.5.6 Operational tables

**`alert_rules`**

| Column | Type |
|---|---|
| `id` | uuid PK DEFAULT `gen_random_uuid()` |
| `rule_name` | text NOT NULL |
| `parameter_code` | text NOT NULL |
| `comparator` | text NOT NULL |
| `limit_value` | double precision NOT NULL |
| `severity` | text NOT NULL DEFAULT `'Warning'` |
| `is_active` | boolean NOT NULL DEFAULT true |
| `created_at_utc` | timestamptz NOT NULL DEFAULT `now()` |

`CONSTRAINT ck_alert_rules_comparator CHECK (comparator IN ('>', '>=', '<', '<=', '='))`.

**`plant_data_log`**

| Column | Type |
|---|---|
| `id` | uuid PK DEFAULT `gen_random_uuid()` |
| `alert_rule_id` | uuid NOT NULL **REFERENCES `alert_rules(id)` ON DELETE CASCADE** |
| `parameter_observation_id` | uuid NULL |
| `material_code` | text NULL |
| `parameter_code` | text NOT NULL |
| `observed_value` | double precision NULL |
| `comparator` | text NOT NULL |
| `limit_value` | double precision NOT NULL |
| `severity` | text NOT NULL |
| `message` | text NOT NULL |
| `logged_at_utc` | timestamptz NOT NULL DEFAULT `now()` |
| `acknowledged_at_utc`, `acknowledged_by` | timestamptz, uuid |

**Unique `(alert_rule_id, parameter_observation_id)`** plus `ON CONFLICT DO NOTHING` in the evaluator. That pair is the whole idempotence mechanism: a second evaluation over the same data logs nothing.

*Note the deliberate denormalisation. The log stores `comparator`, `limit_value`, `parameter_code` and `material_code` rather than only the rule reference, so **editing a rule later does not rewrite history.** A log entry states the condition that fired at the time it fired.*

**`job_log`**: `occurred_at_utc`, `job_type`, `job_name`, `run_id`, `severity`, `message`, `site_code`, `context jsonb`, `channel_code`. Indexes `(job_type, occurred_at_utc DESC)`, `(run_id)`, `(severity)`. One monitor reads this for every family: import, canonical refresh, analysis, ML, supervisor, alert evaluation.

**`ppiq_business_key_definitions`**: `key_code text NOT NULL`, `entity_scope text`, `version_number integer NOT NULL DEFAULT 1`, `created_at_utc`. **`ppiq_business_key_members`**: `definition_id uuid NOT NULL REFERENCES ppiq_business_key_definitions(id) ON DELETE CASCADE`, `member_role text`, `source_field text`, `sort_order integer NOT NULL DEFAULT 0`. *The dictionary that makes cross-source joining auditable rather than magical.*

### 4.5.7 The join graph

Every analytical question resolves along one of five paths.

```
J1  Parameter to defect, same grain
    parameter_observations
      -> material_units            ON parameter_observations.material_unit_id = material_units.id
      -> quality_events            ON quality_events.material_unit_id = material_units.id
      -> defect_catalogs           ON quality_events.defect_catalog_id = defect_catalogs.id
    The base of every same-grain correlation.

J2  Parameter to defect, ACROSS GRAIN         <-- the product's reason to exist
    parameter_observations (parent grain)
      -> genealogy_edges           ON genealogy_edges.parent_material_unit_id = parameter_observations.material_unit_id
      -> material_units (child)    ON material_units.id = genealogy_edges.child_material_unit_id
      -> quality_events (child)    ON quality_events.material_unit_id = material_units.id
    Attributed by genealogy_edges.contribution_weight, weights summing to 1.0 per child.
    Served by the covering index (child_material_unit_id, is_transition, contribution_weight).

J3  Cross-source identity resolution
    staging_records.raw_json
      -> [Transformation Definition + ppiq_business_key_* dictionary]
      -> material_aliases          ON (alias_system, alias_value)
      -> material_units            ON material_aliases.material_unit_id = material_units.id
    The join is DECLARED here, once, and never re-derived downstream.

J4  Loss attribution
    downtime_events
      -> equipment -> areas -> sites
      and equipment -> process_step_executions -> material_units
    Carrying BOTH stopped_minutes and production_impact_minutes.

J5  Provenance walk-back (the audit path)
    any canonical row.import_batch_id
      -> import_batches
      -> source_dataset_definitions
      -> connection_profiles
    Every figure on every screen resolves along J5 to the source it came from.
```

**J2 is the one to show a sceptical engineer.** It is what a business-intelligence tool cannot do without the genealogy table and the weight invariant, and it is the mechanism behind every cross-source claim the product makes.

### 4.5.8 Row-level security, stated as schema

Every tenant-owned table in all three schemas carries `tenant_id NOT NULL`, and enforcement is layered so no single mistake crosses a boundary.

1. **Row-level security enabled on every tenant-owned table**, one policy pattern: `tenant_id = current_setting('ppiq.tenant')::uuid`. The setting is bound per connection from the authenticated principal, **never from client input**.
2. **One resolver** maps principal to tenant. Shared, dedicated, on-premise and air-gapped deployments all use it; a dedicated deployment simply has one tenant and the same policies never exclude a row.
3. **The application composes tenant scope as well**, defence in depth.
4. **An architecture test asserts that every tenant-owned table has its policy**, so a new table cannot ship without one. `FORCE ROW LEVEL SECURITY` is set, so even the table owner is subject to it - which also means **a NULL `tenant_id` makes a row invisible to the application**, and that is why `tenant_id` is `NOT NULL` everywhere rather than defaulted.
5. **Retrieval and export run under the same scope.** There is no side door for search indexes or reports.

### 4.5.9 The index rationale, summarised

| Pattern | Where | Why |
|---|---|---|
| Filtered unique on the provenance pair | `material_units` and every projected entity | Idempotent projection without forbidding rows that have no source identity |
| Covering index on the three genealogy columns | `genealogy_edges` | The feature loader's hot query never touches the heap |
| Partial index on an unprocessed predicate | `staging_records`, `compute_runs` | Stays small no matter how large the table grows |
| Composite `(entity, time)` | observations, events, executions, batches | Every analytical window is a range scan on one entity |
| Composite `(dimension, measure-ordering)` | `(outcome_code, q_value)` on results | Findings are read ranked, so the index serves the sort |
| GIN on `jsonb` | `staging_records.raw_json` | Ad-hoc inspection of a source row during an investigation |
| Vector index | `assistant_chunks.embedding` | Retrieval latency |

---

## 4.6 Credentials, identities and topology

*Per the author's ruling, this document carries operational credentials in full. They are held in this single contiguous section and nowhere else in the document, so that a customer-safe extract is one deletion.*

### 4.6.1 Component topology

| Component | Listens | Role | Talks to |
|---|---|---|---|
| Web application | 5173 dev, 443 via proxy | The interface | API service |
| API service | 5063 dev, behind proxy | The 27 API domains | PostgreSQL, model gateway |
| Workers | none inbound | Import, projection, analysis, ML, supervisor, report pools | PostgreSQL, collector queue |
| PostgreSQL | 5432 | `ppiq_staging`, `ppiq_plant`, `ppiq_meta` | - |
| Reverse proxy | 80, 443 | TLS termination and routing | web, API |
| Collector | customer DMZ, outbound only | One-way push from sources | customer sources, API ingest |
| Model gateway | internal | Assistant serving: self-hosted, private endpoint, customer model | assistant service |

**The direction rule.** The collector connects **outward** to the core. The core never initiates a connection into the operational network. This is what allows a plant automation team to approve the installation without a control-systems risk review.

### 4.6.2 Environments and profiles

| Environment | Database | Selected by |
|---|---|---|
| Local development | empty-start development database | launch profile `local` |
| Demonstration | populated demonstration database | launch profile `presentation` |
| Staging server | server database | server compose file |
| Customer | customer database | customer configuration |

**One branch, two databases, profile-selected.** There is no demonstration branch and no demonstration code path (Chapter 1.6).

### 4.6.3 Emulated source fleet

Six containers mirroring real plant systems across PostgreSQL, Oracle, SQL Server and MySQL, plus file drops, each reachable by the collector exactly as a customer source would be. Service names, ports, database names and credentials are emulation fixtures, versioned outside the product, and are listed in 4.6.5 with the rest.

### 4.6.4 Identity and secret handling

| Class | Where it lives | Rule |
|---|---|---|
| User passwords | `ppiq_meta.users.password_hash` | Memory-hard hash; never reversible |
| Access tokens | Client memory only | Never browser storage; rotating refresh cookie |
| Source credentials | Encrypted vault; `connection_profiles.vault_reference` | Masked on every read-back; never in the browser; never in application configuration |
| Licence token | `ppiq_meta.license_artifacts` | Signed, offline-verifiable; a client-supplied tier override is ignored |
| Server and build credentials | Operator custody | Listed in 4.6.5, per the author's ruling |

Administrative resets are product endpoints that write audit records, never direct database statements (Rule 2). Vendor support access is scoped, time-boxed and audited.

### 4.6.5 Credentials

> **[CREDENTIALS BLOCK - verbatim insertion at assembly]**
>
> This block receives, unaltered and complete: server SSH access and password; the application database host, port, database, username and password; the build-system URL, username and password; the public service URLs; the local development connection string and its environment variables; and the six emulated source service names, ports, database names and credentials.
>
> **Source:** `commands.txt`. It was supplied earlier in this project and has since left my working context, so I cannot reproduce the values from memory and will not invent them. **Re-attach `commands.txt` with the assembly request and this block fills verbatim.**
>
> Everything around this block - the topology, the ports, the profiles, the identity model, the secret-handling rules and the fleet description - is complete above and needs nothing further.
>
> **Standing operations obligation, carried to Chapter 7:** one password is currently shared between the application database and the build system, and the deployment scripts carry a hardcoded server address in fifteen places. The rotation task, the per-environment credential split and the address parameterisation are registered there.

---

*End of Section 4, Part 3. Section 4 is now complete: Part 1 the fifteen data-flow steps to endpoint level, Part 2 the thirty-four pages, Part 3 the schema and the topology.*
