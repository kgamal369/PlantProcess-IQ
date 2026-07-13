# PlantProcess IQ - Product Concept & Constitution (concept.md)
**v1.0 - 12-Jul-2026.** Supersedes rules.txt as the authoritative statement of vision, rules, journey, engine, and boundaries. Sources: rules.txt (Karim), 12-Jul taxonomy clarification, Implementation Audit v3 (12-Jul). Every other document (Roadmap, Doctrine, Identity & Topology, diagrams, website) MUST derive from and cite this file. Where any document conflicts with concept.md, concept.md wins.

---

## 1. Vision
PlantProcess IQ (PPIQ) is a **generic, read-only, evidence-grade process-to-quality intelligence platform** for manufacturing plants of any industry. It installs empty, connects to the customer's existing databases through DB-links, imports their data incrementally, and its Engine **discovers** the relationships between process parameters and quality outcomes - correlations, drivers, predictions - organically, from the data alone, and explains them with citations and honest statistics. It is sold per plant (~EUR100k class), across industries (steel, paper, food/beverage, minerals, tires, aluminum), because nothing inside it knows any industry.

## 2. The Three Product Rules (unchanged, sharpened)

### Rule 1 - GENERIC ONLY
The product contains **no line, word, page, component, schema, or code branch prepared for any specific dataset, industry, or customer.** No demo content ships inside the product. Industry knowledge enters ONLY as customer data (imported) or as user configuration (authored in the product's UIs). Enforcement: the generic-only grep gate over the projection path; the migration-path gate (build fails if scripts/ or seed/ create demo-named objects); both gates falsified once (seen red) before trusted.

### Rule 2 - STARTS EMPTY; DB-LINK IS THE ONLY DOOR
On day one at the customer, the **plant data schema is empty**. Every row of plant data arrives exclusively via DB-link import -> staging -> generic projection. This includes **taxonomy** (clarified 12-Jul): defect catalogs, parameter definitions, and every other reference vocabulary are *plant knowledge* - flat-steel defects differ from paper defects differ from mineral-water defects; every production unit, semi-product, and inspection device has its own vocabulary - so taxonomy tables also start empty and are **imported from the customer's own definition tables through the same pipeline** (DefectCatalog and ParameterDefinition are projector targets).
**The config class is identity only:** Site/plant identity, license, and the sysadmin account (per the PPIQ Admin Golden Rule: sysadmin = SOU support account, auto-provisioned, undeletable; Customer Admin = manual commissioning step). Nothing else is pre-populated. Out-of-band writes (psql inserts) are prohibited in any documented workflow; administrative resets are product endpoints with audit records.

### Rule 3 - THE JOURNEY IS THE PRODUCT
The 15-step canonical journey (section 4) is the acceptance specification. A milestone is done when the journey's steps hold; a demo shows journey steps working, never staged substitutes. Honesty over spectacle: anything not real is stated as roadmap in one scripted sentence, and nothing else is hedged.

## 3. The Emulation Doctrine
The emulated factory (Docker source containers: meltshop-PG, downtime/parsytec-MySQL, pkl-MSSQL, caster/HSM-Oracle) is a **stand-in for a customer's databases, outside the product.** Test data - including deliberately "rigged" statistical patterns (e.g., a pre-verified 9.5x superheat->CRACK_LONG odds ratio with a SCRATCH null control) - is planted in the emulated *source*, never in the product. The product must discover such patterns blind, after import. Emulation assets are versioned, reproducible, and stored durably (never on one laptop); mappings for emulated sources ship as **fixtures outside the product**, never as code branches.

## 4. The Canonical Journey (15 steps) and the Four Low-Code UIs
1. **Connect** - user creates/configures DB-links to customer sources (test-connect, masked credentials, read-only enforcement, throttling: row caps, rate limits, approved windows).
2. **Schedule imports** - each DB-link/dataset binds to an *import job* (type: db-link) with schedule + monitor; registering a dataset makes it due.
3. **Incremental import** - each job cycle pulls only the delta (cursor/watermark per dataset) into the staging layer (ImportBatch + StagingRecord rawJson rows).
4. **UI-1: Data Preparation (1st low-code UI)** - the user prepares, filters, links, and groups staged data and **maps it to the plant schema**: field maps (source column or `const:` literal) per target entity, with restrictions that put the right data in the right table (customer defect rows -> our defect catalog; readings -> parameter observations; units -> material units; genealogy keys -> edges). Output artifact: a **MappingDefinition** the projector consumes. (SQL-view preparation may assist, but a step-4 act is not complete until a MappingDefinition exists.)
5. **Loading jobs** - each mapping binds to a *data-loading job* (schedule + monitor); on import completion the projector runs automatically for the batch's active mapping.
6. **Loaded** - the generic projector writes canonical entities (MaterialUnit, MaterialAlias, ProcessStepExecution, ParameterObservation, QualityEvent, GenealogyEdge, DefectCatalog, ParameterDefinition) - idempotent per batch, typed field-level errors, NOT-NULL coverage from Site config, zero dataset identifiers.
7. **UI-2: Dashboards & Widgets (2nd low-code UI)** - user builds pages, widgets, charts, KPIs; links them to canonical data with lite-SQL helpers (select/filter/group-by as guided click-tools plus expressions/formulas/casting); live preview before commit; sample-data disclosure badge whenever synthetic data is shown.
8. **UI-3: Analysis Authoring (3rd low-code UI)** - user composes statistics/correlation analyses from a **method toolbox** (Pearson, Spearman, chi-square, ANOVA/Mann-Whitney - registry-extensible; drag-and-drop composition is the target UX), selecting parameters/outcomes from canonical data.
9. **Analysis jobs** - each analysis binds to a *data-analysis job* (schedule + monitor); every run passes the readiness gate (e.g., BlockedTooFewRows) which is never weakened to make a run pass.
10. **Results dashboards** - findings render with population, method, effect size, odds ratio, and FDR q-value; deduplicated to latest-run-per-job; nulls are shown honestly ("not a significant driver" is a first-class result).
11. **AI+ML tier (license-gated)** - the same UI-3 authors deeper analyses (anomaly detection, model-based methods) for higher license tiers.
12. **AI+ML jobs** - scheduled + monitored like all jobs.
13. **AI+ML results dashboards** - same honesty contract as step 10.
14. **THE SUPERVISOR (the Engine's brain)** - one premade weekly job that reviews the whole dataset and every Engine job and re-tunes coefficients/windows/configs so all jobs improve. **Guardrails (constitutional):** it may adjust job configurations, feature windows, thresholds *within configured bounds*; it may NEVER weaken readiness gates, refusal logic, or evidence requirements; **every adjustment is a provenance row** (job, parameter, before, after, justification, evidence handle); dry-run mode exists; a known-answer drift test (inject drift -> supervisor corrects) gates its release.
15. **Chatbot (license-gated)** - answers from the Engine: retrieval-grounded (dataset/doc/finding chunks), citation-bearing (every citation resolves to a real canonical row), role-scoped (viewer vs engineer), audit-logged, refusal-first when evidence is absent. Model binding is pluggable (extractive baseline; local LLM e.g. Ollama for on-prem; hosted API where permitted). The assistant reads the Engine; it writes nothing but its audit log.

**UI-4: Plant-Data-Log / Alerting (4th low-code UI)** - user defines rules that log/alert when: a parameter exceeds a limit (threshold), a material takes wrong routing (genealogy deviation), or chemistry is out of expected range (min/max from parameter definitions). An evaluation job scans new observations/events and writes plant_data_log rows; delivery grows from in-app log to email/webhook with acknowledgment workflow. This surface is part of the journey's operational value even though the 15 numbered steps predate it.

## 5. The Engine
Two layers over one governed substrate. **Layer 1 - Data Analysis:** statistical jobs (correlation with FDR control, genealogy graph analysis) over the multi-grain feature/outcome store refreshed from canonical data. **Layer 2 - AI+ML:** deeper model-based jobs, license-gated. **Jobs feed each other:** outputs land in shared stores (findings, knowledge base) that other jobs and the assistant consume; the **Supervisor** (step 14) closes the loop weekly. Scale doctrine: a plant may define 100+ jobs; execution is a bounded-parallelism job executor (per-class pools for import/analysis/ML, statement timeouts, telemetry) - never unbounded, never serialized into drift.

## 6. Platform Boundaries & Non-Negotiables
- **Read-only toward the customer:** PPIQ never writes to customer systems and never controls OT. Every finding is "suspected contributor, not guaranteed root cause."
- **Honest statistics:** multiple-testing correction (q-values) always; sample sizes always shown; controls/nulls reported; no fabricated status anywhere in any UI (honest empty states).
- **Provenance everywhere:** every canonical row carries source_system + source_record_id + import-batch lineage; genealogy attribution weights sum to 1.0 per child (trigger-enforced); is_synthetic separates emulation from production data; the projector accepts only registered connector source_systems.
- **Users, roles, licensing:** role catalog (Admin/Engineer/Viewer at minimum) enforced by named policies down to retrieval scope; license tiers (Standard / Pro Plus / Enterprise) gate features per a published tier->feature matrix at the endpoint layer, with seat limits from the signed license (production Ed25519 keys).
- **Logging:** request/response, audit (immutable), job logs (including mapper field errors), assistant audit - four layers, queryable.
- **Sizing doctrine (v1):** Small ~750k obs/yr -> single VM; Medium ~7.5M -> dedicated PG + PgBouncer + partitioned observations/features + incremental feature refresh; Large ~60M -> LB'd app + read replica. Validated against pilot telemetry.

## 7. Definitions of Done (per milestone class)
- **Presentable (M1-class):** every journey step can be *shown working* in the HMI - screens and a working path suffice; accuracy depth, multithreading, and full role/license enforcement are not required; nothing shown is fabricated.
- **Hardened (M2-class):** this entire document holds at 100% - all rules, all 15 steps + UI-4, supervisor with guardrails, engine scale, roles/licensing/logging - executable end-to-end by a non-author following the runbook.
- **Customer-shaped (M3-class):** scope re-prioritized from customer feedback within 48h of the meeting; multi-industry proof (a second emulated industry ingests through the identical journey with zero app changes).

*End of constitution. Change control: edits to this file require Karim's explicit approval and a version bump; all derived documents re-validate against it.*
