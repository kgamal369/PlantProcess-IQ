# concept.md - Amendment 6: Schema Topology & Persistence Law
**Drafted 20-Jul-2026 | Status: DRAFT awaiting Karim's ratification | Execution: M2 (post-demo) | Origin: 20-Jul schema review (PgAdmin evidence + 000_schemas.sql + scripts 110/130/320/400/430/440 audit)**

Apply to concept.md v1.1 per the amendment procedure (surgical, anchored, version-bumped to v1.2). Nothing in this amendment executes before the customer meeting; the demo runs untouched on the current physical layout.

---

## A6.1 - The ruling

The product persists in **exactly three application schemas** per database, plus PostgreSQL's own `public` restricted to platform infrastructure:

| Schema | Class | Day-one state | Contents |
|---|---|---|---|
| **`ppiq_meta`** | App metadata | tables present; identity + product furniture seeded | users, roles, sessions, license artifacts, tenants, sites identity, jobs (incl. the premade Supervisor weekly job), dashboard/page/widget definitions, widget & chart type catalog, localization, connection profiles, source registrations, audit log, alert rules |
| **`ppiq_plant`** | Customer plant data | tables present; **zero rows** | ALL canonical entities (material units, aliases, process steps, observations, quality events, genealogy, defect catalogs, parameter definitions) AND everything derived from them: feature/outcome store, correlation results, findings, suggestions, value impacts, assistant chunks/index, knowledge base, risk scores, data-quality issues |
| **`ppiq_staging`** | Plant data in transit | tables present; zero rows | ImportBatch, StagingRecord (rawJson), cursor watermarks, schema-drift events, edge-collector buffers - the constitutional staging layer of Journey step 3 |
| `public` | Platform only | - | PostgreSQL extensions, `__EFMigrationsHistory` (or moved), nothing else owned by the application |

**Classification test (binding, resolves every future case):** *if a table exists because of THIS customer's data, it is `ppiq_plant` (or `ppiq_staging` while in transit); if it ships identical to every customer, it is `ppiq_meta`.* Engine outputs derive from customer data and are therefore plant-class - the schemas `canon` and `acquisition` are hereby DISSOLVED into `ppiq_plant` and `ppiq_staging` respectively and cease to exist as names.

## A6.2 - Staging medium ruling

Staging is **in-database**, per concept.md step 3 as written 12-Jul (ImportBatch + StagingRecord rawJson rows): transactional batches, cursor atomicity, provenance joins, RLS. Flat-file dumps are permitted only as an OPTIONAL archival export, never as the pipeline. The name `dump_store` is retired; the schema `ppiq_staging` is the single staging namespace.

## A6.3 - Eradication (extends the existing eradication epic)

1. Every `src_*_shape` table and every `src_*` schema in every application database - emulated source data inside the product, Architecture-B residue of scripts 110/130 - is DELETED. Emulated sources exist only as the external Docker containers per the Emulation Doctrine.
2. The schema `dump_store` is DROPPED after (1).
3. The legacy database `plantprocessiq` is DROPPED after verifying its contents against the M1-12 off-machine archive.
4. Scripts 110/111/130/140/142/665 and `seed/*demo*` are deleted per the already-ratified epic; this amendment adds the schema objects above to its scope.

## A6.4 - Implementation mandate (M2, new v23 IDs, Origin = A6)

1. **DbContext schema mapping.** Every EF entity carries an explicit schema: metadata aggregates -> `ppiq_meta`, plant/derived aggregates -> `ppiq_plant`, staging aggregates -> `ppiq_staging`. No entity without an explicit schema; EF defaults are forbidden by gate (A6.5-G1).
2. **Ordered SQL discipline.** Every `CREATE TABLE`/`CREATE VIEW`/`CREATE FUNCTION` in `Backend/database/scripts/` names its schema explicitly; bare/`public` creations fail the lint gate (A6.5-G2). Existing scripts are migrated or superseded; migrations 740/741-class objects re-target `ppiq_plant`.
3. **Physical move.** One audited migration moves existing tables out of `public` into their ruled schemas (ALTER TABLE ... SET SCHEMA), preserving data, updating every schema-qualified reference (functions, views, the rebuild command M2-18, connection-profile SQL). `public` ends the migration owning zero application tables (A6.5-G3).
4. **The `ppiq_*` script-table inventory.** A generated (never hand-written) inventory from information_schema lists every application table with: origin script, constitutional class, target schema, and for each `ppiq_*` table outside the Domain model a ruling - ENTER the Domain model, remain as ordered-SQL infrastructure with a named justification, or be deleted. The inventory is committed as this amendment's appendix.
5. **Estimate:** 12-18h including the falsify-once runs, the M2-18 rebuild-script regeneration, and a full journey re-certification on the moved layout.

## A6.5 - Gates (each falsified once - seen red - before trusted)

- **G1 `schemaPlacementContract`** (architecture test): every EF entity type declares a schema in {ppiq_meta, ppiq_plant, ppiq_staging}; test fails on any unmapped entity.
- **G2 `sqlSchemaLint`** (repo gate): regex over `Backend/database/scripts/` - any CREATE of a relation/function without an explicit `ppiq_meta.`/`ppiq_plant.`/`ppiq_staging.` qualifier fails the build (allowlist: extensions, EF history).
- **G3 `publicSchemaEmpty`** (runtime probe, certifier row): `SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name NOT IN (allowlist)` must equal 0 on a fresh install.
- **G4 Rule-2 one-liner earned:** after A6, the A2-reviewer proof becomes literal: `SELECT count(*) FROM ppiq_plant.* = 0` on day one.

## A6.6 - What this amendment does NOT change

The Domain model remains the single source of entity truth (the EF core already maps 1:1 to Domain/Entities: Materials, Quality, Process, PlantLayout, Integration, Dashboarding, Security, Audit). The two-database practice (ppiq_app dev / ppiq_presentation demo) is UNAFFECTED - this amendment governs schemas WITHIN a database, and the demo-vs-product doctrine already governs databases. Ruling 1.2.A is hereby resolved by option (2), physical split, scheduled M2.

---

*Ratification: Karim's explicit approval + concept.md bump to v1.2. Until ratified, this document binds nothing and the current layout remains the honest, documented status quo.*
