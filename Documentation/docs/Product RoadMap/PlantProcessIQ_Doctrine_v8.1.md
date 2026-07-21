+-----------------------------------------------------------------------------------------------------------------------------------------------------+
| **SOU INDUSTRIAL SOFTWARE**                                                                                                                         |
|                                                                                                                                                     |
| **PlantProcess IQ**                                                                                                                                 |
|                                                                                                                                                     |
| **Product Vision, Design, Architecture & Realization Doctrine**                                                                                     |
|                                                                                                                                                     |
| **Version 8.1 · The Unified Master Specification (Part I)**                                                                                                  |
|                                                                                                                                                     |
| The deepest engineering reference, the clearest buyer story, and the realistic path from today\'s build to this bar --- in one consistent document. |
|                                                                                                                                                     |
| *Connect Your Plant Data. Understand Your Process.*                                                                                                 |
+-----------------------------------------------------------------------------------------------------------------------------------------------------+

+-----------------------------+-----------------------+--------------------------------------------------------------+
| **PREPARED FOR**            | **DATE**              | **STATUS**                                                   |
|                             |                       |                                                              |
| **Karim · SOU, Düsseldorf** | **3 June 2026**       | **Canonical · v8.1 (18 Jul 2026) supersedes v8 · Part I only; realization lives in Roadmap v9** |
+-----------------------------+-----------------------+--------------------------------------------------------------+

+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Lineage --- six documents, one direction**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| **Compass →** 4-Track Vision (founder intent). **v1 →** Version-Improvement Analysis. **v2/v3 →** first structured doctrine. **v4 →** showed instead of told. **v5 / v5.1 (v6) →** hardened for review + the buyer plain-terms layer. **v7 →** this document. v7 keeps every honest boundary and worked artifact of v4, every clarification and buyer table of v6, recovers the buildable demo detail and worklist density v5 thinned, adds a realistic realization roadmap, and resolves every internal-consistency defect. The 4-Track Vision remains the backbone; §23 proves every point still has a home. |
+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+


**DERIVATION (v8.1):** This document derives from and cites **concept.md v1.1** (the constitution); where they conflict, concept.md wins. It is the SPECIFICATION - how the vision is engineered. It is not the plan (Roadmap v9), not the ledger (Backlog v23), and not a status report (the Implementation Audit lineage owns build-vs-doctrine deltas). The scoped **Interactive Workspace Doctrine v1** (concept Amendment 7) is binding law for every analytics page and is incorporated by reference in §8; its text is single-sourced there and is deliberately NOT duplicated here.

**0 · How to read this document**

v7.0 is written for three readers at once, and engineered so each finds what they need without wading through the others.

  ----------------------------------- --------------------------------------------------------------------------------------------------- -----------------------------------------------------------------------------------------------------------------------
  **Reader**                          **Read this**                                                                                       **You get**

  **Developer / software engineer**   Chapter bodies (§1--§19), worked artifacts, Build notes, and Part II (§20--§23) + Appendix A & I.   The deepest, most buildable spec of the lineage, plus the realistic route from today\'s code to this bar.

  **Customer / non-engineer**         The diagrams, the "in plain terms" panels in each chapter, and Appendices F & H.                    Every clarification of v6, now with branded diagrams, flowcharts and tables --- and an answer to every hard question.

  **Founder / reviewer**              §0, §20--§23 and the traceability.                                                                  Proof nothing in the compass was lost, an honest doctrine-vs-build delta, and one coherent version identity.
  ----------------------------------- --------------------------------------------------------------------------------------------------- -----------------------------------------------------------------------------------------------------------------------

![Figure 1 · The lineage --- every version added a layer; the four-track skeleton is the spine.](media/e8bd5b8be18a152483d2c7b4d3224d039a74bcfe.png "Figure 1 · The lineage — every version added a layer; the four-track skeleton is the spine."){width="6.25in" height="2.4791666666666665in"}

*Figure 1 · The lineage --- every version added a layer; the four-track skeleton is the spine.*

+--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The seven things v7.0 sets out to be**                                                                                                                                                                                 |
|                                                                                                                                                                                                                          |
| 1.  **Deepest for engineers ---** worked SQL, the value formula, method-selection logic, readiness numbers, identity and security specifics --- preserved and extended with Build notes.                                 |
|                                                                                                                                                                                                                          |
| 2.  **All of v6\'s clarity for non-engineers ---** every "in plain terms" panel, now with nine branded diagrams and richer tables.                                                                                       |
|                                                                                                                                                                                                                          |
| 3.  **Everything v5 thinned, recovered ---** the table-level Demo Data Blueprint (Appendix A, melt shop restored), worklist density (Build notes), and internal consistency.                                             |
|                                                                                                                                                                                                                          |
| 4.  **Shiny and high-tech ---** the Dark Industrial Command Center system applied to diagrams, code panels and tables throughout.                                                                                        |
|                                                                                                                                                                                                                          |
| 5.  **A realistic route from here to there ---** Part II is an honest baseline (\~46/100), a phased roadmap and engineering guidance, so a large wording-to-build gap is fine because the way across it is written down. |
|                                                                                                                                                                                                                          |
| 6.  **The best internal consistency ---** one version identity (v7.0) on every page, a self-naming lineage, and an honesty-corrected traceability (§23).                                                                 |
|                                                                                                                                                                                                                          |
| 7.  **Built on the origin ---** the V1 four-track skeleton is explicit in Figure 1 and §23; v7 deepens it, never departs from it.                                                                                        |
+--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**0.1 · Canonical decision record**

This document is the destination, not a status report and not a task list. It defines what "done" means for PlantProcess IQ (PPIQ) so every task, line of code and demo can be measured against one fixed target. Part II then states, honestly, how far today\'s build is from that target and how to close the distance.

  --------------------------------- --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Canonical artifact**            PlantProcess IQ Doctrine v7.0. One document is the source of truth; this is it.

  **Relationship to the compass**   The 4-Track Vision remains founder intent. v7 is its faithful, deepened expression --- nothing dropped; §23 proves it and names every deliberate evolution.

  **Relationship to v4 / v6**       v7 preserves v4\'s worked artifacts and v6\'s clarity, recovers the buildable detail and density v5 compressed, and adds the realization layer none of them had.

  **Source for the task list**      Generated from §22 (gates), sequenced by §20 (roadmap). Every task maps to one gate, one validation method, one evidence artifact.

  **Deployment posture**            One codebase, four topologies: SOU-hosted SaaS, customer-cloud, on-prem, air-gapped. Logical isolation in shared SaaS; physical when dedicated (§16).

  **Data boundary**                 Deterministic engines compute inside the tenant --- no plant data is sent anywhere to be calculated. The assistant LLM is swappable and self-hostable behind a gateway; only minimal, permission-scoped evidence ever leaves (§7.8).

  **Identity**                      Argon2id hashing, mandatory MFA for admins, SSO + SCIM, in-memory access tokens with HttpOnly refresh cookies. The localStorage access token is retired (§10.2).

  **Licensing**                     Cryptographically signed, offline-verifiable entitlements (Ed25519); subscription default, perpetual for air-gapped; tamper-evident (§9.3--9.4).

  **The non-negotiable**            PPIQ must feel generic, configurable, secure, evidence-based, demo-safe and buyer-review-ready --- without pretending to be a MES, SCADA, L2, or guaranteed-root-cause AI.
  --------------------------------- --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Core target statement**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| **PlantProcess IQ is a configurable, read-only process-to-quality intelligence layer for manufacturing plants.** It connects fragmented plant data through an OT-safe collector, stages source-shaped copies in a time-series-optimized store, maps them into a canonical manufacturing model through an explicit, versioned mapping engine, composes dynamic dashboards and widgets from data (never hardcoded), runs transparent simple analysis and credible advanced analysis, quantifies business impact in euros, generates deterministic evidence-ranked suggestions, and answers through a grounded AI assistant that explains the math but never invents it --- all without replacing the customer\'s MES, L2, SCADA, PLC or BI, all under enterprise-grade security, identity and licensing, and all provable on the customer\'s own data. |
+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**0.2 · Design decisions and their rationale**

A doctrine shows its reasoning so future contributors do not relitigate settled trade-offs.

  -------------------------------- ------------------------------------------------------------------------------------------------------- ----------------------------------------------------------------------------------------------------------------------------------
  **Decision**                     **Chosen path**                                                                                         **Why (and what was rejected)**

  **Where data lives first**       Source-shaped staging, mapped to canonical later.                                                       A plant won\'t remodel its databases for us. Rejected: forcing data into canonical on import --- breaks on the first odd source.

  **How joins are authored**       Explicit, versioned safe-SQL views + a business-key dictionary.                                         Real plants have no universal key. Rejected: automatic join inference --- unauditable, fails silently.

  **How intelligence escalates**   Data → Information → Simple → Advanced → Suggestions → Assistant.                                       Trust is built in stages; users see transparent math before AI. Rejected: raw-data-to-chatbot.

  **Who computes numbers**         Deterministic engines compute; the assistant narrates with citations.                                   Every figure reproducible. Rejected: the LLM doing arithmetic or ranking.

  **How pages are built**          Composed from Page + Widget definitions via the HMI.                                                    One generic mechanism, not 100k endpoints. Rejected: bespoke pages per chart.

  **What the demo is**             The first real release on prepared data, built entirely through the HMI.                                If we hardcode, we fake ourselves and miss real bugs. Rejected: a separate demo app or hardcoded screens.

  **Where the model runs**         A swappable gateway: self-hosted default for on-prem, zero-retention endpoint for SaaS, or BYO model.   Data sovereignty is non-negotiable. Rejected: hardwiring one public LLM and shipping raw plant data to it.

  **Multi-tenant vs on-prem**      One codebase: TenantId + RLS for SaaS; same code single-tenant when dedicated.                          Two forks diverge and double the bugs. Rejected: a separate on-prem product.

  **Time-series at scale**         Time-series store --- hypertables/columnar, compression, downsampling, retention.                       Process parameters are billions of rows. Rejected: a naive row table in vanilla Postgres.

  **How we reach data**            An Edge Collector in the DMZ pushes one-way; CDC/replica preferred over polling.                        Plant IT won\'t let an app poll the live historian. Rejected: the core opening connections into OT.

  **Configurator skill**           No-code visual mapper + templates for the common case; safe-SQL for the long tail.                      A SQL-only product can\'t scale to many plants. Rejected: a SQL author for every dashboard.

  **How value is proven**          Demo proves the engine; a Proof-of-Value pilot proves the signal on the customer\'s data.               Synthetic data proves only that the machinery runs. Rejected: claiming the demo proves real discovery.
  -------------------------------- ------------------------------------------------------------------------------------------------------- ----------------------------------------------------------------------------------------------------------------------------------

**0.3 · What v7.0 unifies --- the master change ledger**

The index and the proof: every strength of every prior version, and every gap the lineage audit exposed, resolved here.

  ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- ---------------------------------
  **Brought into v7.0**                                                                                                                                                                                                                                                                 **From / fixing**

  **Full hardened spec: data egress & model governance, identity, signed licensing, OT-safe acquisition, historian/OPC connectors, schema-drift, time-series scale, accessibility & i18n, coverage honesty, blended-heat genealogy, security & compliance, ROI & competitive story.**   v5 / v6 --- preserved in full

  **Buyer "in plain terms" panels + nine branded diagrams and flowcharts in every relevant chapter.**                                                                                                                                                                                   v6 --- preserved & enriched

  **Table-level Demo Data Blueprint with the EAF/Ladle melt shop restored as a first-class source (Appendix A).**                                                                                                                                                                       Recovers v4 / fixes the v5 loss

  **Worklist density --- Build notes in every build-bearing chapter restore the "what exactly to do."**                                                                                                                                                                                 Recovers v4

  **Realization Roadmap (§20), Doctrine-vs-Build delta (§21), Build Backlog Mapping (Appendix I).**                                                                                                                                                                                     New in v7

  **One version identity everywhere, a self-naming lineage, honesty-corrected traceability (the MES/QES→packs pivot named).**                                                                                                                                                           Fixes v6 consistency defects
  ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- ---------------------------------

**PART I**

**The Product --- what PPIQ must become**

Chapters 1--19 are the destination. Each carries the engineering body, the worked artifacts, a buyer "in plain terms" panel, and --- where it builds something --- a Build note that says concretely what to construct.

**1 · Executive product target & the North Star**

PPIQ is a generic plant-intelligence platform that lets each plant connect its own sources, define its own mappings, build its own pages, monitor its own jobs, and investigate its own quality, downtime and KPI questions --- with evidence. It carries the user from fragmented raw data to trustworthy understanding, and never asks for blind faith in a black box.

Everything serves one emotional target --- the North Star. Three sensations land in the first demo; the fourth is signed against at the pilot.

  -------------------------------------------------------------- -------------------------------------------------------------------------------- ----------------------------------------------------------------------------------------------------------------------
  **Sensation**                                                  **What it means**                                                                **How it is proven**

  **"It already speaks my plant."**                              It understood my data, my keys, my line --- without me remodelling anything.     Connect a real source, map it, walk a genealogy thread from a coil back to its melt --- on the customer\'s own keys.

  **"It told me something I didn\'t know --- and proved it."**   A non-obvious quality/downtime driver, with the evidence and method shown.       Run an inspection job on a real defect; surface a stable, stratified, evidence-ranked contributor with a euro range.

  **"It won\'t embarrass me."**                                  I can click anything in front of my boss and nothing breaks or lies.             Every button works; every slow call shows progress; the assistant refuses to guess and cites every number.

  **"And it pays for itself."**                                  The money it saves is bigger than what it costs, and I can see the arithmetic.   A worked business case on the customer\'s own pilot data, payback period, every input drill-through (§14.5, §19).
  -------------------------------------------------------------- -------------------------------------------------------------------------------- ----------------------------------------------------------------------------------------------------------------------

**1.B · In plain terms --- who buys it and the pain it removes**

  ----------------------- ---------------------------- ----------------------------------- ----------------------------
  **Buyer**               **Main pain**                **What PPIQ solves**                **Demo proof**

  **Plant Manager**       Losses and downtime          Euro impact and priority actions    Monthly value report

  **Quality Manager**     Defects and claims           Defect-driver investigation         Edge-crack / defect story

  **Process Engineer**    Manual Excel investigation   Heat-to-coil genealogy & analysis   One-click genealogy

  **IT / OT Manager**     Security risk                Read-only Edge Collector            No write-back architecture

  **CFO / Procurement**   ROI uncertainty              Bounded value model                 Payback model on your data
  ----------------------- ---------------------------- ----------------------------------- ----------------------------

**2 · Product boundary & the honesty contract**

PPIQ is commercially ambitious but technically honest. Its strength is not that it replaces plant systems --- it connects and explains the data those systems already produce. Per the compass, the honesty contract is the product\'s integrity and its strongest sales asset: a skeptical engineer trusts "suspected contributor, here is the evidence" over "root cause."

  --------------------------------------------------- -------------------------------------------------------------------------------
  **Forbidden language**                              **Approved language**

  **Guaranteed root cause**                           Suspected / likely contributor; evidence-ranked factor

  **AI-powered prediction; production-ready AI**      ML readiness; statistical learning; risk scoring; correlation

  **Live Oracle/MSSQL ready today (unless proven)**   Connector capability & availability shown per source

  **We replace MES / L2 / SCADA / BI**                We connect around existing systems and turn fragmented data into intelligence

  **Autonomous optimization**                         Decision support and guided investigation with human approval
  --------------------------------------------------- -------------------------------------------------------------------------------

+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The honesty mechanism --- enforced in code, not just promised**                                                                                                                                                       |
|                                                                                                                                                                                                                         |
| **Provenance handles.** Every numeric or qualitative claim carries an evidence handle resolving to a finding, job run, dataset, source table, or document section. A claim without a resolvable handle is not rendered. |
|                                                                                                                                                                                                                         |
| **No-fabrication guard.** The assistant assembles answers from tool results and retrieval only; any sentence containing a number without a handle is rejected before display.                                           |
|                                                                                                                                                                                                                         |
| **Anti-facade rule.** Seeded demo rows are tagged origin = seed and are never presented as live computation. Learning jobs recompute on demo data so the demo proves the engine, not a fixture.                         |
+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**2.1 · What PPIQ connects out to --- and what it never does**

PPIQ never writes to control systems, but it is not a dead end: notifications (email, Teams, Slack, webhook), case & action tracking against the KPI a suggestion targeted, and a read-only export/integration API for the customer\'s own BI. Every outbound path is a message, an export, or a webhook --- never a setpoint or command.

+---------------------------------------------------------------------------------------------------+
| Plant systems PlantProcess IQ People / BI                                                         |
|                                                                                                   |
| MES L2 SCADA Historian \--(read-only)\--\> engines \--(results)\--\> dashboards \| reports \| API |
|                                                                                                   |
| \^ \|                                                                                             |
|                                                                                                   |
| \|\_\_\_\_\_\_\_\_\_\_\_\_\_ NO setpoint / recipe / command EVER flows back \_\_\_\_\_\_\_\_\_\_X |
+---------------------------------------------------------------------------------------------------+

+---------------------------------------------------------------------------------------------------------------------------------------------------+
| **Read-only is absolute**                                                                                                                         |
|                                                                                                                                                   |
| A security and trust feature, not a limitation --- it is why the plant\'s automation team can approve PPIQ without a control-systems risk review. |
+---------------------------------------------------------------------------------------------------------------------------------------------------+

**3 · End-to-end architecture**

The architecture is layered; each layer owns one responsibility and is independently testable. No layer fakes another\'s job: the source layer knows nothing of dashboards, the widget layer cannot bypass security, and the assistant cannot invent facts outside approved retrieval and tools.

![Figure 2 · The eleven layers --- data flows down, trust flows up.](media/535cec373c458e8e068d20a531447e8bcc2210d4.png "Figure 2 · The eleven layers — data flows down, trust flows up."){width="6.25in" height="6.760416666666667in"}

*Figure 2 · The eleven layers --- data flows down, trust flows up.*

Two cross-cutting realities shape the design. Storage is split by shape: relational copies in PostgreSQL, high-frequency parameters in a time-series store (§16.3). And the same codebase runs in two isolation models --- shared SaaS (TenantId + RLS) and single-tenant dedicated/on-prem/air-gapped --- with one identical entitlement resolver and isolation rule set.

+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The three-contracts rule**                                                                                                                                                                                                                  |
|                                                                                                                                                                                                                                               |
| Every capability has three matching contracts: a **backend contract** (data + endpoint), a **frontend UX contract** (workflow + states), and a **validation contract** (the proof test). A feature is complete only when all three are green. |
+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**3.B · In plain terms --- who does what during onboarding**

  ---------------- ----------------- --------------------------- ---------------------- --------------------
  **Step**         **Customer IT**   **Customer process team**   **SOU consultant**     **System**

  **Connection**   Approves source   Defines data need           Configures connector   Tests connection

  **Mapping**      Reviews schema    Defines meaning             Builds mapping         Validates coverage

  **Analysis**     Provides data     Reviews findings            Tunes configuration    Computes results
  ---------------- ----------------- --------------------------- ---------------------- --------------------

**4 · Generic data source, linking & joining --- the heart**

This is the heart of the product and the one place the compass explicitly asked for a complete design: "I don\'t have a full vision how this mapping could be done --- design it effective, advanced, professional, detailed and generic." v4 designed the engine and showed it working end to end; v5 added the parts a plant\'s IT and DBA insist on before a single byte moves. v7 preserves both, restores the buildable detail, and pins each piece to a gate and a wave (§20--§22).

**4.1 · Onboarding workflow**

-   Admin creates a DB / file / historian / stream link and picks a provider (§4.7); the system tests connectivity before save, masks credentials on read-back, and stores secrets encrypted at rest in the Edge Collector vault.

-   Admin selects source objects (tables, views, tags, sheets, files) to stage, and sets sync cadence per object --- from 2 minutes to days --- with optional off-hour windows.

-   The system profiles each object: row count, primary-key candidate, timestamp candidate, nullable columns, types, sample preview, and snapshots the schema for drift detection (§4.9).

-   A source-shaped staging object is created under namespaced tenant/source naming; a delta cursor is configured (numeric index, timestamp, composite key, or full-snapshot).

-   Every sync writes an import batch: rows read/inserted, duration, watermark, errors and audit metadata. Staging stays source-shaped --- canonical transformation happens later, in the mapping layer.

**4.2 · The layered joining model**

PPIQ never assumes one universal physical key, because real plants do not have one. Joins start from business keys and grow stronger through normalization, mapping views, genealogy and a confidence score. Every join is explicit, versioned, testable and auditable.

  ----------------------------- ---------------------------------------------------------------------------- -----------------------------------------------------------------------------------------------------
  **Join layer**                **Purpose**                                                                  **Example (flat-steel demo)**

  **Source natural key**        Use the keys already in customer systems.                                    HeatId, SlabId, CoilId, LadleId, TundishId, MouldId, SequenceId, SampleId

  **Business-key dictionary**   Declare which columns mean the same business concept across systems.         HSM.coil_output.piece_no ≡ SurfaceInspection.defect_events.material_id (after normalization)

  **Normalization rule**        Standardize values before joining.                                           Trim, upper-case, strip prefix "C-", drop leading zeros, parse dates, convert units

  **Mapping-authored view**     Data engineer authors safe SQL joining staged tables into canonical facts.   Caster.slab → HSM.coil → Parsytec.defect, emitted as canonical quality_event rows

  **Genealogy spine**           Parent-child material relations end to end.                                  Heat → Ladle → Tundish → Mould → Strand → Slab → Coil → downstream piece

  **Temporal-proximity rule**   Join by credible time/equipment windows when direct keys are absent.         A downtime event around a caster sequence affecting slabs in the same interval

  **Confidence score**          Grade each join: exact / normalized / inferred / ambiguous.                  Exact key = high; time-window inferred = medium; conflicting keys = rejected, never silently merged
  ----------------------------- ---------------------------------------------------------------------------- -----------------------------------------------------------------------------------------------------

**4.3 · Worked example --- from two foreign keys to one canonical fact**

The compass\'s exact scenario: a coil the HSM Oracle system calls "C-0044170" is the same physical coil the Parsytec MySQL device calls "44170". PPIQ reconciles and joins them explicitly, not magically. Step 1 --- the business-key dictionary entry declares the concept and its normalization:

+--------------------------------------------------------------------------------+
| business_key: CoilId                                                           |
|                                                                                |
| members:                                                                       |
|                                                                                |
| \- source: HSM.coil_output.piece_no norm: strip_prefix(\'C-\'), ltrim(\'0\')   |
|                                                                                |
| \- source: PARSYTEC.defect_events.material_id norm: cast_int -\> to_text       |
|                                                                                |
| \- source: CASTER.slab.coil_ref norm: ltrim(\'0\')                             |
|                                                                                |
| rule: members must resolve EQUAL after norm; conflict -\> reject (never merge) |
+--------------------------------------------------------------------------------+

Step 2 --- the mapping-authored safe-SQL view produces canonical quality_event facts by walking slab → coil → defect:

+--------------------------------------------------------------------------------+
| CREATE VIEW canon.quality_event AS \-- read-only, SafeSqlValidator-checked     |
|                                                                                |
| SELECT norm_coil(h.piece_no) AS coil_id, \-- normalized business key           |
|                                                                                |
| s.heat_id AS heat_id, \-- genealogy back-link                                  |
|                                                                                |
| d.defect_code, d.position_mm, d.severity,                                      |
|                                                                                |
| d.inspection_time AS event_time,                                               |
|                                                                                |
| \'normalized\' AS join_confidence                                              |
|                                                                                |
| FROM stg_parsytec.defect_events d                                              |
|                                                                                |
| JOIN stg_hsm.coil_output h ON norm_coil(h.piece_no) = norm_coil(d.material_id) |
|                                                                                |
| JOIN stg_caster.slab s ON s.coil_ref = h.piece_no                              |
|                                                                                |
| WHERE d.inspection_time \>= :window_start; \-- bounded, row-limited, validated |
+--------------------------------------------------------------------------------+

+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **What the SafeSqlValidator and resolver guarantee here**                                                                                                                                   |
|                                                                                                                                                                                             |
| **Read-only.** INSERT/UPDATE/DELETE/DROP/DDL-on-source are rejected; only SELECT and CREATE-VIEW over staging are allowed.                                                                  |
|                                                                                                                                                                                             |
| **Bounded.** An implicit row limit and statement timeout are applied; an EXPLAIN is run before publish.                                                                                     |
|                                                                                                                                                                                             |
| **Typed errors, not generic failure.** A wrong reference returns a specific resolver error the HMI shows precisely --- NoSuchView, NoSuchColumn, InvalidAggregateForType, AmbiguousJoinKey. |
+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**4.4 · Worked example --- a KPI as a first-class SQL view**

KPIs are not hardcoded metrics; they are mapping-authored views, versioned like any mapping, and consumed by both widgets and the KPI-contributor learning job. Example --- First-Pass Surface Yield (FPSY) by grade and day:

+------------------------------------------------------------------------------------+
| CREATE VIEW canon.kpi_first_pass_surface_yield AS                                  |
|                                                                                    |
| SELECT date_trunc(\'day\', q.event_time) AS day,                                   |
|                                                                                    |
| h.steel_grade AS grade,                                                            |
|                                                                                    |
| 1.0 - ( COUNT(\*) FILTER (WHERE q.severity \>= 2)::numeric                         |
|                                                                                    |
| / NULLIF(COUNT(DISTINCT q.coil_id),0) ) AS first_pass_yield                        |
|                                                                                    |
| FROM canon.quality_event q                                                         |
|                                                                                    |
| JOIN canon.heat h ON h.heat_id = q.heat_id                                         |
|                                                                                    |
| GROUP BY 1, 2; \-- registered as KPI \'FPSY\'; unit = ratio; target set per tenant |
+------------------------------------------------------------------------------------+

**4.5 · The mapping workbench & tiered authoring**

  ----------------------------- ------------------------------------------------------------------- ---------------------------------------------------------------------------------
  **Tab**                       **User goal**                                                       **Critical UX / validation**

  **Source Catalog**            See connected tables/views/tags/files and their profile.            Row preview, types, count, watermark, last import, data-quality warning

  **Business-Key Dictionary**   Define Heat/Slab/Coil/Sample/Equipment/Event keys across systems.   Detect duplicates, nullable keys, inconsistent formats, conflicting mappings

  **Canonical Entity Mapper**   Map source fields/views into canonical entities.                    Required-field coverage, type compatibility, unit conversion, sample validation

  **SQL View Builder**          Author safe views for joins, KPIs and calculated fields.            SafeSqlValidator, read-only, EXPLAIN plan, row limit, visual↔SQL toggle

  **Genealogy Builder**         Define parent-child material paths.                                 Cycle detection, orphan detection, path preview heat→coil

  **Validation & Publish**      Validate a mapping version and publish to demo/production.          Dry run, row counts, rejected records, warnings, rollback to prior version
  ----------------------------- ------------------------------------------------------------------- ---------------------------------------------------------------------------------

Authoring is tiered so a plant does not need a SQL expert to get value, but can express anything when it does --- the answer to "does my configurator have to be a programmer?" is, for the common case, no.

  --------------------------- ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- ---------------------------------
  **Authoring tier**          **What it offers**                                                                                                                                                                                    **Who uses it**

  **No-code visual mapper**   Drag-drop field mapping, point-and-click joins on detected keys, and a library of industry and KPI templates. Covers the common majority with zero SQL.                                               Process / quality engineer

  **Assisted**                Profiling suggests keys, normalization and join candidates with confidence; the assistant can draft a view from a plain-language description for a human to review (execution stays deterministic).   Engineer + reviewer

  **Expert SQL**              The safe-SQL view builder --- SafeSqlValidator, EXPLAIN, row limits --- for the complex long tail.                                                                                                    Data engineer (customer or SOU)
  --------------------------- ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- ---------------------------------

**4.6 · OT-safe connection topology & the Edge Collector**

A plant\'s caster and rolling mill run on production-critical systems inside a segmented OT network, and the DBAs will not allow an outside application to query the live historian. PPIQ sits at the information level (L3/L4), never inside the control level (L2), and reaches sources through a collector the customer controls.

![Figure 4 · OT-safe acquisition --- one-way push from a customer-controlled collector; no inbound path to control.](media/c4ae4597db22bf58197aa39439b42aa5f73372cc.png "Figure 4 · OT-safe acquisition — one-way push from a customer-controlled collector; no inbound path to control."){width="6.25in" height="2.8854166666666665in"}

*Figure 4 · OT-safe acquisition --- one-way push from a customer-controlled collector; no inbound path to control.*

  ---------------------------------------- --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Topology element**                     **Role**

  **Edge Collector (DMZ / read zone)**     A lightweight agent the customer runs near the sources. It pulls, applies the source-impact budget, and pushes one-way into PPIQ staging. It is the only component that ever holds source credentials.

  **One-way / data-diode option**          For high-security plants the push channel can be unidirectional; the PPIQ core never initiates a connection back into the OT network.

  **Capture strategy (preferred order)**   \(1\) Change-data-capture via logical replication or a log reader; (2) a read replica or standby; (3) throttled direct reads in off-peak windows --- chosen per source with the DBA.

  **Source-impact budget**                 Per-source row caps, statement timeouts, concurrency and rate limits, and approved time windows, so PPIQ\'s read load on any production system is bounded and signed off in advance.

  **Secrets**                              Source credentials live only in the Edge Collector\'s encrypted vault, masked on read-back, never in the browser or the core application config.
  ---------------------------------------- --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

**4.7 · The connector catalog --- generated from the live Connector Truth Contract, 18 Jul 2026**

*(v8.1: this table is regenerated from the code and the demo evidence, resolving the v8-internal contradiction between the old §4.7 ("Relational GA") and the old §21 ("only MSSQL/MySQL exist"). Source of truth: `Backend/PlantProcess.Infrastructure/Connectors/` - one `IDataSourceReader` implementation per family, cursor-tracked, throttled, locale-safe (ISO-8601 invariant), null-cursor-safe.)*

| Family | Implementation (in tree) | Read mode | Honest status, 18 Jul 2026 |
|---|---|---|---|
| **PostgreSQL** | `PostgreSqlConnector` | bounded incremental query, cursor watermark | **LIVE-PROVEN** - meltshop source imported through the HMI (1,802 heats) |
| **MySQL** | `MySqlConnector` | same | **LIVE-PROVEN** - parsytec/downtime profiles connected, schema-verified |
| **Oracle** | `OracleConnector` | same | **LIVE-PROVEN via API 17-Jul** (HSM_COILS, CC_HEATS); HMI discovery pending the PPIQ_SRC schema field (M1-19) |
| **SQL Server** | `MsSqlConnector` | same | **CONNECTED** - pkl profile registered; not on the demo path |
| **CSV / Excel files** | `CsvConnector`, `ExcelConnector` | scheduled pull / drop | **BUILT**, not demo-exercised |
| **Process historians** | `OpcUaHistorianConnector` | time-ranged native API | **CODE PRESENT**, uncertified - status per the Truth Contract below |
| **Industrial protocols** (OPC-DA gw, MQTT/Sparkplug, Kafka) | - | subscribe/stream | **PLANNED** |
| **Enterprise & lab** (SAP RFC/OData, REST/GraphQL, MES/LIMS) | - | scheduled API pull | **PLANNED** |

+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Connector Truth Contract**                                                                                                                                                                                                                                                                                                                                                                                                                                          |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| Every connector declares its capability and its real availability honestly. A connector designed but not yet certified for a particular source version says so in the UI --- the forbidden claim "live Oracle ready today" is replaced by an explicit, per-source capability state, and that state is tested (§13). High-frequency historian and OPC data lands in the time-series store with configurable downsampling and retention (§16.3), not a naive row table. |
+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**4.8 · Initial historical backfill & source-load protection**

-   The backfill is a throttled, resumable, checkpointed batch --- never one giant query against the primary. It honours the same source-impact budget as live sync.

-   History can come from a DBA-provided dump or export, a read replica/standby, or partitioned range reads inside an agreed maintenance window --- whichever the DBA prefers.

-   Backfill is idempotent and watermark-tracked; it can pause and resume; its progress and source-impact are visible in the Jobs Monitor (§11).

-   Backfill is also the maturity accelerator: loading several months of history makes readiness gates pass on day one instead of waiting months (§6.3).

**4.9 · Upstream schema-drift detection & mapping health**

-   Every staged object\'s profile --- columns, types, key candidates --- is snapshotted; each sync compares the live shape against the snapshot.

-   On drift (new, renamed, removed column, or type change), PPIQ raises a typed Schema-Change event, flags the dependent mapping views, and --- configurable --- pauses dependent canonical imports rather than producing incorrect facts.

-   A Mapping Health panel shows, per mapping version, which views are green, degraded or broken, and exactly why (the typed resolver error from §4.3).

-   Nothing fails silently: a broken join surfaces NoSuchColumn / AmbiguousJoinKey precisely, with the affected view and the next safe step.

**4.B · In plain terms --- what we connect to and why IT can approve it**

  ------------------------- ---------------------------------------------- ----------------------------------- ----------------
  **Family**                **Examples**                                   **Read mode**                       **Status**

  **Relational DB**         PostgreSQL, SQL Server, Oracle, MySQL          CDC / replica / bounded query       GA

  **Files & drops**         CSV, Excel, Parquet, JSON over SFTP / folder   Scheduled pull or drop-triggered    GA

  **Process historian**     PI Web API, IP.21, AVEVA, Proficy              Native historian API, time-ranged   Targeted

  **Industrial protocol**   OPC-UA, MQTT / Sparkplug, Kafka                Subscribe / stream into staging     Planned / beta

  **Enterprise & lab**      SAP, REST / GraphQL, MES / LIMS APIs           Scheduled API pull                  Planned
  ------------------------- ---------------------------------------------- ----------------------------------- ----------------

+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §4 (Wave C: connectors & acquisition)**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| **Today.** CSV/Excel connectors are functional; MSSQL and MySQL connectors exist; a SafeSqlValidator with the CTE-alias fix passes its test suite; DB-Link and Table-Browser UIs and a delta-import execution service are in place.                                                                                                                                                                                                                                                                                                                                       |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| **To reach this bar.** Build the Edge Collector agent (one-way push + source-impact budget) --- gate G11; certify the relational connectors per source version and add one historian (PI Web API) to GA --- gate G1; implement schema-drift snapshotting + the Mapping Health panel --- gate G2; wire the business-key dictionary normalization functions and the confidence-graded join resolver. The worked SQL above is the acceptance fixture: a coil must resolve both genealogy directions on demo keys, and a bad mapping must return a typed error and roll back. |
+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**5 · Canonical domain & semantic model**

The canonical model is the stable language of PPIQ --- generic enough to cross industries, specific enough to support process-quality investigation. Locations and equipment are recursive, so any plant topology fits without schema change. The flat-steel demo populates it, but nothing in the schema is steel-specific.

  ------------------------------------------------------------- -------------------------------------------------------------------------------- -------------------------------------------------------------------------------------------
  **Canonical concept**                                         **Meaning**                                                                      **Key relationships**

  **Tenant**                                                    Customer/company boundary.                                                       Owns sites, users, licenses, links, mappings, pages, data

  **Site / Area / Equipment**                                   Plant, recursive location, recursive machine hierarchy.                          ParentAreaId / ParentEquipmentId --- unlimited layers, line → machine → station → subunit

  **Material Unit**                                             Any tracked physical unit.                                                       Heat, slab, coil, strip, pack, batch, serialized product

  **Genealogy Edge**                                            Parent-child material relation.                                                  Heat→slab, slab→coil, coil→downstream

  **Process Step Execution**                                    An operation on a material unit.                                                 Links material, equipment, operation, time, crew/context

  **Parameter Observation**                                     A measured/calculated parameter.                                                 Temperature, speed, oxygen, electricity, pressure, width, thickness, chemistry

  **Quality Event**                                             Defect, inspection, lab result or decision.                                      Links material, time, defect catalog, severity, decision

  **Downtime Event**                                            Stoppage event (see §5.2).                                                       Separates equipment-stoppage from production-stoppage minutes

  **KPI Definition / Risk Score / Suggestion / Value Impact**   Named calculation; rule/statistical result; recommended action; euro exposure.   Each carries its model, inputs, evidence and date
  ------------------------------------------------------------- -------------------------------------------------------------------------------- -------------------------------------------------------------------------------------------

**5.1 · The genealogy golden thread**

The single most convincing demo moment is walking from a surface defect on a finished coil all the way back to the chemistry of the melt that produced it --- on the customer\'s own key names. The canonical genealogy spine makes this one navigable thread.

![Figure 5 · The genealogy golden thread --- one click from a defect to the melt, both directions, across all eight sources.](media/312d44599f1b83cb7ae1a473645b0bb309af0391.png "Figure 5 · The genealogy golden thread — one click from a defect to the melt, both directions, across all eight sources."){width="6.25in" height="2.5833333333333335in"}

*Figure 5 · The genealogy golden thread --- one click from a defect to the melt, both directions, across all eight sources.*

  -------------------------- -------------------------------------- --------------------------------------------------
  **Canonical entity**       **Demo instance**                      **Parent link**

  **Heat**                   H-3361 (EAF melt, grade S355)          root of the thread

  **Ladle / LF treatment**   L-2207                                 Heat H-3361

  **Tundish (sequence)**     T-118 (sequence SEQ-44)                Ladle L-2207

  **Mould / Strand**         M-3 / Strand 2                         Tundish T-118

  **Slab**                   S-44170-A                              cast on Strand 2

  **Coil**                   C-0044170 (HSM piece_no "C-0044170")   rolled from Slab S-44170-A

  **Quality events**         5 edge-crack defects, severity ≥ 2     inspected on the coil; trace back to Heat H-3361
  -------------------------- -------------------------------------- --------------------------------------------------

**5.2 · Downtime --- equipment stop vs production impact**

Naively costing downtime by raw equipment-stopped minutes either wildly over- or under-states the loss. PPIQ models two distinct quantities, and the value engine uses the right one.

  ---------------------------- -------------------- ----------------------------------------------------------------- -------------------------
  **Worked case**              **Equipment stop**   **Propagation**                                                   **Production stop**

  **HSM roll change**          22 min               TF buffer (9 slabs) + caster 5.2→3.8 m/min                        0 min (fully absorbed)

  **Caster water-pump trip**   3 min                Strand freeze → SQ-221 abort + tundish skull → sequence rebuild   ≈312 min (4--6 h class)
  ---------------------------- -------------------- ----------------------------------------------------------------- -------------------------

+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The value engine costs impact, not raw stops**                                                                                                                                                                                                                                                                                                                    |
|                                                                                                                                                                                                                                                                                                                                                                     |
| Downtime cost (§7.5) is computed on production-impact minutes, not equipment-stopped minutes --- so a buffered 22-minute mill stop costs nothing, while a 3-minute caster trip that kills a sequence costs hundreds of minutes. Downtime-contributor learning (§7.2) is weighted by true production impact, so the pump trip is flagged and the roll change is not. |
+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**5.3 · Blended provenance --- mixed-heat / casting-transition genealogy**

In continuous casting, consecutive heats are cast in one sequence; at the join a transition slab contains steel from two heats. Blaming a defect on a transition coil on a single heat is metallurgically wrong, and a sharp customer will test exactly this. PPIQ models blended provenance instead of faking a clean answer: parent_heat_id (one row per parent), weight (fractions sum to 1), is_transition, and provenance_confidence (exact · modeled · uncertain). Asked "which heat caused this defect," the assistant answers honestly that the coil is transition material blending H-3361 (≈70%) and H-3362 (≈30%) --- attribution shared, not singular.

**5.4 · Linkage & data-coverage transparency**

If 12% of coils cannot be linked to a heat, an analysis that silently drops them is misleading --- the missing rows might be exactly where the problem lives. PPIQ refuses silent survivorship: every analysis states its population (e.g. "computed on 5,142 of 5,604 coils --- 91.8%; 462 excluded: 7.1% missing heat link, 1.1% rejected ambiguous key"), a Data Coverage panel shows linked vs unlinked counts and the reason per exclusion, and excluded records are inspectable, never hidden.

**5.5 · Defect-label quality & cross-device harmonization**

Surface-inspection labels are themselves imperfect --- false detections, missed defects, device-specific codes. PPIQ tracks and surfaces inspection coverage (an uninspected coil is not a defect-free coil), carries each device\'s confidence/severity so low-confidence detections can be down-weighted or filtered, and maps device codes to one canonical defect taxonomy while preserving the original code for traceability.

+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §5 (Wave B/C: canonical depth)**                                                                                                                                                                                                                                                                                                                                           |
|                                                                                                                                                                                                                                                                                                                                                                                             |
| **Today.** A canonical structure exists; functional DataQuality and RiskDashboard pages are in place; the genealogy spine and blended-provenance/coverage/label-quality models are largely unstarted.                                                                                                                                                                                       |
|                                                                                                                                                                                                                                                                                                                                                                                             |
| **To reach this bar.** Implement the recursive area/equipment model and the genealogy edge table with cycle/orphan detection --- gate G2; build the Data Coverage panel and the versioned defect catalog; add blended-provenance weighting. Acceptance: the demo coil C-0044170 walks both directions, and a transition coil reports weighted shared attribution rather than a single heat. |
+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**6 · Data-to-insight intelligence pipeline**

Insight escalates in disciplined stages so trust is earned before AI is involved. Business users trust stages 3--4; engineers can audit every step. The pipeline is intentionally staged from transparent simple analysis to advanced analysis, then suggestions, then assistant --- so PPIQ never becomes a black box.

![Figure 3 · The six-stage pipeline and the maturity curve --- simple analysis from day one, advanced findings as readiness gates turn green.](media/6866aabc009617718f933066603ced90f3271ec7.png "Figure 3 · The six-stage pipeline and the maturity curve — simple analysis from day one, advanced findings as readiness gates turn green."){width="6.25in" height="3.28125in"}

*Figure 3 · The six-stage pipeline and the maturity curve --- simple analysis from day one, advanced findings as readiness gates turn green.*

  ----------------------------- ------------------------------- ------------------------------------------------------------ -----------------------------------------------------------------
  **Stage**                     **User question**               **System responsibility**                                    **Output**

  **1 Raw data**                What data do we have?           Connect, import, stage source-shaped data.                   Source catalog, staging tables, import status

  **2 Validated information**   Can it be trusted and joined?   Profile, validate, map, build canonical facts + genealogy.   Data-quality score, canonical entities, genealogy path

  **3 Simple dashboard**        What happened?                  Counts, averages, trends, thresholds, KPI views.             Defect rates, downtime minutes, production counts, KPI charts

  **4 Advanced dashboard**      What patterns may explain it?   Statistical/ML-assisted analysis + ranking.                  Correlations, suspected contributors, risk, segment comparisons

  **5 Suggestions**             What should I do next?          Evidence-linked, impact-ranked recommendations.              Action cards with owner, impact, confidence, evidence

  **6 AI assistant**            Explain and guide me.           Retrieve approved evidence; run approved tools via chat.     Grounded, cited answer + next-step workflow
  ----------------------------- ------------------------------- ------------------------------------------------------------ -----------------------------------------------------------------

**6.1 · Simple-analysis contract**

Transparent math only --- count, sum, average, min, max, median, stdev, ratio, rate, trend, threshold, distribution, comparison, status. Every metric shows its formula, dataset, filters, time window and last-refresh time. Simple dashboards are the first source of truth --- easy to explain, no AI trust required, available from day one with zero history requirement.

**6.2 · Advanced-analysis contract**

Runs only after data-readiness checks pass (§7.3). Every result shows method, sample size, filters, confidence/stability, excluded records, data-quality warnings and the honesty caveat. Effect size first; Benjamini-Hochberg FDR (flag only q \< 0.05); bootstrap stability with a percentage shown; stratification by grade, line and product to avoid Simpson\'s-paradox confounds; the method named with the finding; and the language is always "suspected contributor," never "root cause."

**6.3 · Cold-start & the maturity curve**

  --------------- --------------------------- ------------------------------------------------------------------------------------------------------------------------------------------------
  **Phase**       **Elapsed (no backfill)**   **What works**

  **Day 1**       0                           Sources connected, raw catalog visible, simple dashboards and KPIs live (no history). Readiness meters show progress toward advanced analysis.

  **Week 1--2**   \~2 weeks                   Canonical mapping, genealogy and simple KPIs stabilize; first trends become meaningful.

  **Month 1**     \~1 month                   Enough outcome events accumulate for the first advanced findings as gates turn Ready.

  **Month 3+**    \~3 months                  Full advanced analysis, value quantification and suggestion maturity; rare-defect analyses still need more history.
  --------------- --------------------------- ------------------------------------------------------------------------------------------------------------------------------------------------

+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The readiness meter is a feature, not a limitation**                                                                                                                                                                                                                                                                                                                                                                 |
|                                                                                                                                                                                                                                                                                                                                                                                                                        |
| The product never shows nothing while a gate is Blocked: it shows simple analysis, the readiness meter ("22 of 60 heats needed for this analysis"), and an honest "collecting data" state. A backfill of several months collapses the timeline (§4.8). It turns the vague worry "when will the AI be useful?" into a concrete countdown --- and sets honest expectations that protect the relationship after the sale. |
+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**6.B · In plain terms --- what works on day one vs later**

  -------------- ------------------------------------------------------------------- -------------------------------------
  **When**       **What the buyer sees**                                             **AI needed?**

  **Day 1**      Connected sources, simple dashboards, KPIs, genealogy once mapped   No

  **Month 1**    First advanced findings with evidence as gates pass                 Stats, not AI arithmetic

  **Month 3+**   Mature suggestions and euro impact ranges                           Assistant explains; engines compute
  -------------- ------------------------------------------------------------------- -------------------------------------

**7 · ML & AI design architecture --- the deep chapter**

The division of labour is the architecture\'s strongest trust feature: deterministic engines compute everything --- simple analysis, advanced statistics, value, risk and suggestions --- and the LLM assistant only explains, with citations. The model never does arithmetic, never ranks a finding, and physically cannot render a number it cannot cite. A smaller or larger model changes how helpfully it explains, never whether the numbers are right.

![Figure 6 · The responsibility boundary --- engines compute and rank; the assistant retrieves, explains and cites; it never invents a number.](media/e958169fac6fd4ed597b4e5813ded70fb15519a9.png "Figure 6 · The responsibility boundary — engines compute and rank; the assistant retrieves, explains and cites; it never invents a number."){width="6.25in" height="2.9791666666666665in"}

*Figure 6 · The responsibility boundary --- engines compute and rank; the assistant retrieves, explains and cites; it never invents a number.*

**7.1 · Intelligence components**

  ------------------------------------------ ----------------------------------------------------------------------------------------------------
  **Component**                              **Responsibility**

  **Feature Store**                          Curated, versioned features per canonical entity for analysis and ML.

  **Readiness Gate**                         Decides Ready / Partial / Blocked per investigation from data-sufficiency thresholds (§7.3).

  **KPI Engine**                             Computes KPI views deterministically; powers widgets and contributor analysis.

  **Correlation Engine**                     Runs method-appropriate statistics with FDR, effect size, stability and stratification.

  **Value Engine**                           Converts findings and downtime into bounded euro impact ranges, or abstains (§7.5).

  **Risk Scorer**                            Scores material / process risk and exposes the reasons behind each score.

  **Suggestion Engine**                      Generates deterministic, evidence-ranked, impact-ranked next actions (§7.6).

  **Assistant Retrieval Index**              Tenant-scoped index of approved findings, definitions and documents for grounding.

  **Assistant Tool Layer + Model Gateway**   Lets the assistant call engines and retrieval, then route to the LLM under a data boundary (§7.8).
  ------------------------------------------ ----------------------------------------------------------------------------------------------------

**7.2 · The ML jobs**

All learning runs as scheduled batch jobs on worker nodes, visible in the Jobs Monitor (§11), and recomputes on demo data so the demo proves the engine, not a fixture: defect-contributor learning, downtime-contributor learning (production-stop weighted, §5.2), KPI-contributor learning, overall plant learning, on-demand inspection (generates a page), and assistant indexing.

**7.3 · Method selection, readiness gates & statistical discipline**

The statistic is chosen to fit the data, not forced, and is always named with the finding. The engine ranks by effect size first, never by p-value.

  --------------------------------------------- -------------------------------------- --------------------------------------------------------------
  **Data situation**                            **Method auto-selected**               **Mandatory reporting**

  **Numeric ↔ numeric, monotonic**              Spearman rank correlation              Coefficient, n, time window, filters

  **Numeric ↔ numeric, nonlinear**              Mutual information                     Normalized score; "association, not causation"

  **Categorical ↔ categorical**                 Cramér\'s V                            Effect size + n

  **Binary outcome ↔ numeric**                  Point-biserial                         Coefficient + n

  **Many candidate parameters, collinearity**   Lasso / regularized screen + VIF       Selected features, regularization, excluded collinear groups

  **Any ranked finding**                        Bootstrap stability + stratification   Stable/unstable flag, confidence band, survives strata?

  **Known / business rules**                    Rule-based risk scoring                Rule inputs, thresholds, exact calculation
  --------------------------------------------- -------------------------------------- --------------------------------------------------------------

  ------------------------------------ --------------- --------------- ---------------
  **Readiness check**                  **Ready**       **Partial**     **Blocked**

  **Independent heats in window**      ≥ 60            30--59          \< 30

  **Outcome events (e.g. defects)**    ≥ 40            15--39          \< 15

  **Class balance (minority share)**   ≥ 10%           3--10%          \< 3%

  **Data freshness (last import)**     ≤ cadence       ≤ 2× cadence    \> 2× cadence

  **Required-field completeness**      ≥ 95%           85--95%         \< 85%
  ------------------------------------ --------------- --------------- ---------------

+-------------------------------------------------------------------------------------------------------------------------+
| **Statistical-honesty discipline (always on)**                                                                          |
|                                                                                                                         |
| **Effect size first.** Rank by effect/impact, never by p-value. A tiny effect with a small p is not a headline.         |
|                                                                                                                         |
| **Multiple-testing control.** Benjamini-Hochberg FDR (q \< 0.05) across the parameter scan; report q-values, not raw p. |
|                                                                                                                         |
| **Confounding control.** Stratify by grade, width, shift, crew, equipment, route; report whether a finding survives.    |
|                                                                                                                         |
| **Stability.** Bootstrap the ranking; flag contributors that do not survive resampling.                                 |
+-------------------------------------------------------------------------------------------------------------------------+

**7.4 · A worked honest finding**

On the demo data, an inspection-driver job on edge-crack defects (grade S355, 30 days) surfaces two suspected contributors --- each with its method and evidence --- and pointedly refuses a third:

+----------------------------------------------------------------------------------------+
| Investigation: edge-crack severity \| grade S355 \| 30 days \| n = 71 heats, 142 coils |
|                                                                                        |
| Readiness: READY (heats 71, events 58, balance 14%, freshness OK)                      |
|                                                                                        |
| Suspected contributors (evidence-ranked --- NOT root cause):                           |
|                                                                                        |
| 1\. Tundish superheat \> 35 C Spearman r_s = 0.41 q = 0.004 stability 86%              |
|                                                                                        |
| 2\. Mould-level stdev \> 3.5 mm MI(norm) = 0.23 q = 0.011 stability 79%                |
|                                                                                        |
| Excluded: casting speed (r_s = 0.08, q = 0.61; and collinear, VIF 7.8)                 |
|                                                                                        |
| Survives stratification by width and shift: YES                                        |
|                                                                                        |
| Honesty: evidence-based decision support; association, not guaranteed cause.           |
+----------------------------------------------------------------------------------------+

+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The honesty is in what it leaves out**                                                                                                                                                                                               |
|                                                                                                                                                                                                                                        |
| Casting speed showed a weak, non-significant association and was not surfaced. A tool that surfaced it anyway would generate the false leads that destroy an engineer\'s trust. Restraint under statistical discipline is the feature. |
+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**7.5 · The value / impact model --- the commercial lever, made concrete**

A finding becomes a decision only when it carries a euro figure. The value engine computes a deterministic, bounded, abstaining range from per-tenant cost inputs, and every number is drill-throughable. This is the single highest-ROI commercial feature --- and, today, the largest unbuilt one (§20--§21).

+---------------------------------------------------------------------------------------+
| impact_eur_per_month =                                                                |
|                                                                                       |
| SUM over affected material( tonnes \* penalty_eur_per_t )                             |
|                                                                                       |
| \+ ( attributable_production_stop_minutes \* downtime_eur_per_min ) (§5.2 weighted)   |
|                                                                                       |
| \+ ( yield_loss_tons \* grade_premium_per_ton )                                       |
|                                                                                       |
| penalty_eur_per_t = downgrade_delta OR scrap_value                                    |
|                                                                                       |
| Every term cites its inputs. Missing assumption -\> \'insufficient basis\' (abstain). |
|                                                                                       |
| Output is a RANGE from the assumption bands, never a single \'guaranteed\' number.    |
+---------------------------------------------------------------------------------------+

  ------------------------------------------- --------------------------- ---------------------------------------------
  **Assumption (per-tenant, HMI-editable)**   **Demo default**            **Used in**

  **Good coil value (S355)**                  €620 / t                    Volume valuation

  **Downgrade penalty (prime → secondary)**   €70 / t                     Defect impact

  **Scrap loss (recovery-adjusted)**          ≈ full value                Defect impact

  **Downtime cost --- caster / HSM**          €1,800 / min · €450 / min   Downtime impact (production-impact minutes)

  **Grade premium (S355 → higher)**           €40 / t                     Yield impact

  **Energy**                                  €95 / MWh                   Energy KPI impact
  ------------------------------------------- --------------------------- ---------------------------------------------

+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Worked impact --- paired with the §7.4 finding**                                                                                                                                                                                                                                                                                                                                                                                                                    |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| The edge-crack / superheat finding on S355, combining downgraded tonnes and the associated production-impact minutes over a month, yields a range of **€28k--€56k per month, point estimate ≈ €42k** --- and every figure in that range is clickable back to the heats, coils and events behind it. If cost/ton is unconfigured, the engine returns "insufficient basis" rather than a number. The presentation layer forbids the words "guaranteed" and "will save." |
+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**7.6 · The suggestion record --- deterministic, structured, workflow-bound**

  ----------------------------- -------------------------------------------------------------------------------------
  **Field**                     **Meaning**

  **id / title**                Stable identifier and one-line action statement.

  **suspected_contributor**     The factor the finding implicates.

  **evidence_handles**          Resolvable links to the finding, job run and dataset.

  **impact_eur_range**          Low--high euro estimate from the value engine.

  **confidence**                Graded from effect size + stability + coverage.

  **recommended_action**        Inspect / monitor / tune / validate --- never "fix, guaranteed."

  **affected_scope / status**   Grade, line, period · open / assigned / in-progress / validated / dismissed (§2.1).
  ----------------------------- -------------------------------------------------------------------------------------

**7.7 · The assistant grounding runtime**

The concrete sequence behind every answer: (1) the user asks; (2) it resolves against tenant-scoped retrieval; (3) the assistant selects and calls deterministic tools with parameters --- it never computes itself; (4) results return carrying evidence handles; (5) the provenance check rejects any sentence containing a number without a resolvable handle (the no-fabrication guard); (6) the answer renders with inline citations and an offer of the next safe step.

**7.8 · Model serving, data boundary & provider governance**

This is the single question most likely to stall an enterprise deal, so the answer is committed and unambiguous. The analytical engines are ours and run inside the tenant --- your numbers are never sent to a third party to be computed. Only the conversational assistant uses an LLM, behind a Model Gateway with three serving modes and a strict data-minimization contract.

  ------------------------------------- ---------------------------------------------------------------------------------- ------------------------------------------------------------- -----------------------------------------------
  **Serving mode**                      **Where the model runs**                                                           **Default for**                                               **Data egress**

  **Self-hosted / in-tenant**           Open-weights model on the customer\'s own GPU, inside their network.               Enterprise, on-prem, air-gapped (and wherever a GPU exists)   None --- nothing leaves the network.

  **Vendor-managed private endpoint**   Business-tier model API under a signed DPA, zero-retention, no-training.           SaaS Pro / Pro Plus                                           Only the question + scoped evidence chunks.

  **Bring-your-own-model**              The gateway points at the customer\'s Azure OpenAI / Bedrock / on-prem endpoint.   Customers with an approved LLM                                Stays within the customer\'s chosen provider.
  ------------------------------------- ---------------------------------------------------------------------------------- ------------------------------------------------------------- -----------------------------------------------

+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Data-minimization contract**                                                                                                                                                                                                                                                                                                                          |
|                                                                                                                                                                                                                                                                                                                                                         |
| **What may leave (private-endpoint mode only):** the question text, the specific retrieved evidence chunks the question needs, and the tool schemas.                                                                                                                                                                                                    |
|                                                                                                                                                                                                                                                                                                                                                         |
| **What never leaves:** credentials, full source/staging tables, other tenants\' data, or anything beyond the permission-scoped retrieval. A redaction pass can mask customer-identifying tokens; a per-tenant "no external model calls" toggle forces self-hosted serving; every call is audited (which model, what classification left, token counts). |
+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**7.9 · Assistant evaluation & model-version governance**

The residual risk is not fabricated numbers --- the guard handles those --- but misinterpretation, mis-summary, or the wrong tool. PPIQ governs this with a golden Q&A set per release (scored on groundedness, tool-selection, correctness, refusal-appropriateness), an automated faithfulness/entailment check, tool-selection tests, a causal-overreach lint ("root cause," "guaranteed," "optimal"), a regression gate (a model cannot ship unless it matches or beats the prior version\'s scores), model-version pinning, and human-in-the-loop thumbs feedback.

**7.10 · Configuring the assistant from the HMI · 7.11 · Limits of correlation**

Per principle #1, if it cannot be configured from the HMI it is not done --- including the assistant: which tools it may use per role and tier, which knowledge sources are indexed, a plant glossary and synonyms, guardrail phrases and verbosity. And two sharp questions get honest, designed answers: for unmeasured confounders, findings are framed as suspected contributors, every visible confounder is stratified and stated, and when a likely confounder is unmeasured the assistant says so; for KPI reconciliation, definitions are versioned and transparent, PPIQ can align to the customer\'s authoritative definition and show a reconciliation view, and positions itself as the investigative layer, not the regulatory system of record.

+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §7 (Wave B: the value engine is the biggest unbuilt feature)**                                                                                                                                                                                                                                                                                                                                                                                                                         |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| **Today.** ML readiness and a risk dashboard exist; the L4 statistical methods (Spearman, mutual information, Lasso, VIF, bootstrap) and --- above all --- the Value Model are entirely unstarted. This is the single largest gap between this doctrine and the code.                                                                                                                                                                                                                                   |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| **To reach this bar.** Build the Correlation Engine with the §7.3 method table + FDR + stratification + bootstrap --- gate G5; build the Value Engine implementing the §7.5 formula with the per-tenant cost table and the abstain path --- gate G5; wire the deterministic Suggestion Engine and the assistant no-fabrication guard + evaluation harness --- gate G4. Acceptance fixture: the §7.4 finding and the €28k--€56k range reproduce on demo data, and the assistant emits no uncited number. |
+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**7.12 · THE SUPERVISOR --- the Engine's brain (journey step 14; v8.1 addition)**

*(v8.1: the single largest omission of v8 - zero occurrences in 1,215 lines while being the constitution's keystone and M2's flagship. Specified here per concept.md v1.1 §4 step 14.)*

One premade weekly job reviews the whole dataset and every Engine job, and re-tunes coefficients, feature windows, and configurations so all jobs improve over time. It is the closed loop that makes the Engine an engine rather than a batch of scripts.

**Guardrails (constitutional, non-negotiable).** The supervisor may adjust job configurations, feature windows, and thresholds *within configured bounds*. It may NEVER weaken readiness gates, refusal logic, or evidence requirements - the honest-abstain machinery is outside its write scope by construction, not by convention.

**Adjustment provenance.** Every adjustment is a provenance row: job, parameter, before-value, after-value, justification, evidence handle. *(Build note: the storage table for these rows does not exist yet - it ships with the M2 loop.)* A dry-run mode produces the full adjustment list without applying it. Release is gated by a known-answer drift test: inject drift into the emulated source, the supervisor must detect and correct it.

**Honest status, 18 Jul 2026.** v0 exists since 14-Jul: `SupervisorEndpoints` + `SupervisorReportPage` - a real report from `ml_correlation_results_v2` via knowledge-base upsert, an honesty line, and a monitor row. NOT yet: schedule, tuning actions, adjustment-provenance storage. v0 is the honest framing artifact; the full closed loop is the M2 P0 keystone (Backlog v23). Grade [B].

**8 · UI / UX, information architecture & performance**

The HMI is the product. It must feel fast, never dead, never lie about state, and be usable by every role, in every language the customer needs, on the devices they actually use. It is a dark-industrial command center: calm, technical, high-trust, enterprise-grade.

**Binding reference (v8.1):** every analytics page, dashboard, widget, and chart is additionally governed by the **Interactive Workspace Doctrine v1** (concept Amendment 7 - the seven standards: universal interactivity, global cross-filtering, one visual language, widget window controls, the component library, low-code page authoring, true grid behavior), enforced by the `interactiveWorkspaceContract` gate once installed. That document is the single source; this section does not restate it.

**8.1 · Information architecture**

  ------------------------ ------------------------------------------------------------------------------------------------------
  **Area**                 **Pages**

  **Command Center**       Home --- plant health, freshness, active jobs, critical suggestions, risk overview

  **Investigate**          Material Investigation · ML / Correlation · Advanced Analysis · Suggestions · AI Assistant

  **Build**                Page Builder · Widget Builder · Dynamic Dashboard pages

  **Admin · Data**         DB Links · Source Objects · Importing Data · Schema Configuration (mapping workbench) · Jobs Monitor

  **Admin · Governance**   User / Role Admin · License Admin · Audit

  **Operate / Report**     Reports · scheduled exports · Demo Lifecycle · Data Coverage · Mapping Health · Alerts & cases
  ------------------------ ------------------------------------------------------------------------------------------------------

**8.2--8.4 · Shell, edge states, interaction standards**

Persistent left navigation by IA area, a command palette, and an always-visible, unmistakable tenant + environment (demo / production) badge. Every page handles five states explicitly:

  --------------------- ----------------------------------------------------------------------------------------
  **State**             **Standard**

  **Empty**             Explains what to do next with a primary action --- never a blank panel.

  **Loading**           Skeletons and progress; long operations show elapsed time and a cancel.

  **Error**             A typed, specific message and the next safe step --- never a generic "could not load."

  **Partial / stale**   Shows coverage and last-updated; warns when data is stale or low-coverage (§5.4).

  **Success**           The data, with its formula and evidence reachable in one click.
  --------------------- ----------------------------------------------------------------------------------------

No dead controls --- every button does something or is disabled with a visible reason. Every operation over \~400 ms shows progress; destructive actions confirm and show their dependencies first (§12.3).

**8.5 · Performance budgets**

  ----------------------------------------------- ---------------------------------------------------------
  **Operation class**                             **Budget**

  **Interactive UI action (open page, filter)**   \< 800 ms p95

  **Standard query-backed widget**                \< 3 s p95, with skeleton

  **Heavy analytical query**                      \< 30 s with live progress; otherwise promoted to a job

  **Learning / import / report**                  Asynchronous job, tracked in the Jobs Monitor
  ----------------------------------------------- ---------------------------------------------------------

**8.6--8.8 · Accessibility, i18n, mobile**

PPIQ targets WCAG 2.1 AA / EN 301 549: full keyboard navigation with visible focus, ARIA on every control, data-table fallbacks for charts, colour never the only signal, a contrast-audited dark theme plus a light high-contrast theme, and respected reduced-motion / font-scaling. It localizes fully --- first-class English, German and Arabic including right-to-left layout --- with locale-aware number/date/unit formatting, a per-tenant/per-user unit system, and UTC-stored timestamps shown in the user\'s or site\'s timezone. A responsive, tablet-optimized shop-floor mode covers the consume-and-act path (read dashboards, acknowledge alerts, view a finding, work a suggestion\'s status).

**8.9 · Concurrency & collaboration**

The sharp question --- "two users on the same page, one edits, what does the other see?" --- gets a designed answer, not last-write-wins: optimistic concurrency with version stamps and a clear conflict dialog (merge / overwrite / cancel), live presence ("User X is editing this page"), and immutable published versions with editing on a draft so viewers always see a stable version.

+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §8 (Wave A/D: UX hardening, i18n, accessibility)**                                                                                                                                                                                                                                                                                                             |
|                                                                                                                                                                                                                                                                                                                                                                                 |
| **Today.** The admin shell, optimistic-save hook pattern across major admin components, and core pages exist. **To reach this bar.** Enumerate every page against the five edge states and the no-dead-button rule --- gate G6; add the WCAG-audited light theme and Arabic RTL --- gate G14; implement optimistic-concurrency conflict dialogs and draft/publish immutability. |
+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**9 · License, commercial model & entitlements**

Licensing is one backend truth driving both API and UI. v7 makes firm what earlier versions left "illustrative": the tiers, the commercial model, and --- critically --- how a licence is issued, activated, renewed and protected, including with no internet at all.

**9.1 · Tiers & entitlements**

*(v8.1 - PENDING RULING 1.2.B: three documents disagree on the tier canon - rules.txt (Lite/Pro/Pro Plus/Enterprise, 15/25/40/50k), concept.md §6 (Standard/Pro Plus/Enterprise), and current commercial doctrine (12/28/50k deposits + monthly). The table below is retained from v8 UNRESOLVED; the frontend type union still carries the oldest scheme plus a Rule-1-violating "Demo" tier. One M2 task aligns concept §6, this section, the frontend unions, and the tier-to-feature matrix to the ruling once made. Until then, no document may quote tier names or prices as settled.)*

  ------------------------ ------------- ------------------ ------------------ ---------------------------------
  **Capability**           **Lite**      **Pro**            **Pro Plus**       **Enterprise**

  **Named users**          3             15                 40                 custom

  **Connected sources**    demo / CSV    5                  15                 unlimited

  **Pages / dashboards**   5             50                 200                custom

  **Advanced analysis**    ---           limited            full               full

  **Suggestions**          ---           yes                yes                yes

  **Assistant serving**    ---           private endpoint   private endpoint   self-hosted / BYOM

  **Reports & exports**    basic         yes                scheduled          scheduled + API

  **Deployment**           SaaS          SaaS               SaaS / cloud       any, incl. on-prem & air-gapped

  **Support**              community     standard           priority           SLA-backed
  ------------------------ ------------- ------------------ ------------------ ---------------------------------

**9.2--9.5 · Resolution, commercial model, signed licensing, overage**

One backend resolver computes effective rights = f(licence tier, role); both API and UI consult it, so a gate cannot be bypassed by calling the API directly, and an unavailable feature is shown disabled with an upgrade path. Commercially: annual subscription by default (multi-year discounts), with a perpetual licence plus annual maintenance available for air-gapped / on-prem; priced per-site base + tier with add-ons, as a plant platform, not a per-seat BI tool; clear pre-expiry warnings, then a configurable grace period (default \~30 days, read-only), then read-only of existing dashboards --- customer data is never destroyed by expiry.

+----------------------------------------------------------------------------+
| LICENSE OBJECT (Ed25519-signed JWS, offline-verifiable)                    |
|                                                                            |
| tenant, tier, seat_cap, source_cap, env_cap,                               |
|                                                                            |
| issue_date, expiry_date, feature_flags\[\], allowed_deployment_mode        |
|                                                                            |
| -\> verified against SOU public key; works fully air-gapped, no internet   |
|                                                                            |
| -\> entitlements come ONLY from the signed token, never an editable DB row |
|                                                                            |
| -\> broken/absent signature =\> clearly-communicated invalid-licence state |
+----------------------------------------------------------------------------+

+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **A customer cannot flip their own tier in an on-prem database**                                                                                                                                                                                                                                                |
|                                                                                                                                                                                                                                                                                                                 |
| The tier lives in a signed token the application verifies, not in a row anyone can UPDATE. Soft caps are the default --- a warning and upsell as a cap is approached, never silent data loss or a hard stop mid-work; hard caps apply only where a contract requires them, and are always communicated clearly. |
+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**10 · User, role, access & tenant governance**

**10.1 · Roles & the access matrix**

  ---------------------------------------------- ------------------------------------------------------------------------------------
  **Role**                                       **Scope & key permissions**

  **Tenant Owner**                               Owns the tenant; billing and all settings.

  **Plant Admin**                                Connections, mappings and users within a site.

  **Data Engineer**                              Authors mappings, KPI views and safe SQL.

  **Process / Quality / Reliability Engineer**   Investigates, builds dashboards, runs analysis, works suggestions.

  **Operator**                                   Reads dashboards, acknowledges alerts, updates case status.

  **Viewer / Executive**                         Reads dashboards and reports.

  **Commercial Admin**                           Licence and commercial settings.

  **Support (SOU) / Super Admin (SOU)**          Scoped, time-boxed, fully audited assistance / platform operations across tenants.
  ---------------------------------------------- ------------------------------------------------------------------------------------

+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Tenant isolation is absolute**                                                                                                                                                                                                                                                                                      |
|                                                                                                                                                                                                                                                                                                                       |
| Every row carries a TenantId and row-level security enforces that no query, API call or assistant retrieval can cross a tenant boundary in shared SaaS. In dedicated, on-prem and air-gapped deployments there is exactly one tenant and physical isolation. Cross-tenant leakage is an explicit test in §13 and §18. |
+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**10.2 · Identity & authentication**

A security reviewer flags authentication first. **v8.1 verified correction (18-Jul):** the localStorage access token named by v7/v8 is RETIRED - `AuthContext.tsx` holds auth state in memory only, and contract test P2-T05 (`AuthContext.inMemory.contract.test.tsx`) gate-enforces that neither localStorage nor sessionStorage ever appears in it. The remaining §10 commitments (HttpOnly refresh rotation, Argon2id, MFA, SSO/SCIM) stand.

  ------------------------ ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Area**                 **Commitment**

  **Password storage**     Argon2id (memory-hard), per-user salt + server-side pepper, configurable cost --- never plaintext, never reversible (bcrypt only as legacy fallback).

  **Tokens & sessions**    Short-lived access tokens in memory only; refresh tokens in HttpOnly, Secure, SameSite cookies with rotation and server-side revocation. The localStorage access token is retired.

  **MFA**                  TOTP and WebAuthn / passkeys; enforced by policy per tenant and mandatory for admin roles.

  **Password policy**      Configurable minimum length, breach-list (k-anonymity) check, lockout / backoff on failed attempts.

  **SSO & provisioning**   SAML 2.0 and OIDC; SCIM 2.0 for automatic provisioning/deprovisioning; JIT provisioning; group-to-role mapping.

  **Session policy**       Configurable concurrent sessions (single active session per named seat to enforce licensing), idle/absolute timeouts, active-sessions view, force-logout.
  ------------------------ ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §10 (Wave A: retiring the localStorage token is the highest-priority security item)**                                                                                                                                                                                                                                                                                                                 |
|                                                                                                                                                                                                                                                                                                                                                                                                                        |
| **Today (corrected v8.1, 18-Jul).** The localStorage token is retired and gate-enforced (P2-T05). Access control is deny-by-default via the static endpoint matrix (`PlantAccessControl.cs`); Role/Access depth beyond it is shallow.                                                                                                                                                                                                                                                 |
|                                                                                                                                                                                                                                                                                                                                                                                                                        |
| **To reach this bar.** Move the access token to memory + HttpOnly refresh cookie with rotation and revocation; add Argon2id hashing, MFA for admins, and the password policy --- gate G12; build the role matrix and per-endpoint authorization with cross-tenant tests, then SSO/SCIM --- gate G5. Retiring the localStorage token is what makes the named-seat caps of §9 real rather than theoretical; do it first. |
+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**11 · Jobs, scheduling & operational control**

Everything heavy is a job: visible, retryable, idempotent, and isolated from interactive traffic. The Jobs Monitor is the operational heart of the product.

  ------------------------ ----------------------------------------------------------------------------------------
  **Job family**           **What it does**

  **Sync / import**        Pull deltas and historical backfills from sources via the Edge Collector (§4.6, §4.8).

  **Mapping validation**   Dry-run and validate a mapping version before publish (§4.5).

  **Analytics / ML**       KPI-contributor, defect-driver, risk, stability and value jobs (§7.2).

  **Indexing**             Refresh the assistant retrieval index.

  **Report**               Generate and deliver scheduled reports (§15).

  **Demo reset**           Idempotently re-seed and recompute the demo (§14).
  ------------------------ ----------------------------------------------------------------------------------------

Lifecycle: queued → running → succeeded / failed / cancelled, with a full run history (rows read, duration, errors). Reliability: idempotency keys, exponential backoff with bounded retries, and per-job concurrency limits so jobs never starve interactive traffic. Observability: per-job SLOs and alerting on failure (§2.1) --- a stuck or failed job is visible and explained, never silent.

**12 · Active dynamic widget & script layer**

Pages are composed from data, not hardcoded. A widget binds to a mapping-authored view through a small declarative script and carries its evidence handle --- so everything on screen is traceable. This is the mechanism that makes the demo\'s golden rule (§14) buildable: one generic engine, not 100k endpoints.

**12.1 · The script layer (worked)**

+-----------------------------------------------------------------------+
| widget:                                                               |
|                                                                       |
| type: timeseries                                                      |
|                                                                       |
| title: \"First-Pass Surface Yield --- S355\"                          |
|                                                                       |
| source_view: canon.kpi_first_pass_surface_yield                       |
|                                                                       |
| filter: { grade: \"S355\" }                                           |
|                                                                       |
| x: day y: first_pass_yield                                            |
|                                                                       |
| evidence: kpi:FPSY \# provenance handle --- every value is traceable  |
+-----------------------------------------------------------------------+

**12.2 · The widget library & builder**

  ------------------------------- ------------------------------------------------
  **Widget type**                 **Use**

  **Time-series / trend**         KPI and parameter trends over time

  **Distribution / histogram**    Spread of a measurement or defect position

  **Pareto / ranking**            Top defect codes, top downtime reasons

  **Correlation / scatter**       Two parameters, with the statistic shown

  **Genealogy explorer**          Walk the heat → coil thread (§5.1)

  **Finding / suggestion card**   An evidence-linked result with its euro range

  **KPI tile**                    A single number with target and trend
  ------------------------------- ------------------------------------------------

Pick a type, bind a source view, set filters and axes, attach the evidence handle, preview, and save as a versioned widget --- then compose widgets into dashboards and pages, all from the HMI. Every definition (view, widget, KPI, dashboard, report) has a dependency graph; before deletion PPIQ shows exactly what references the item and blocks or cascades with confirmation (deletes are soft, with restore); and published findings keep an immutable snapshot of the definitions they used, so a result from three months ago reproduces even after the underlying view changes.

+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §12 (Wave C: config-from-HMI completeness)**                                                                                                                                                                                                                                                                                                                                               |
|                                                                                                                                                                                                                                                                                                                                                                                                             |
| **Today.** A KPI Definition UI, SQL View Editor, and admin sub-components exist (the 2,362-line monolith was split into six). **To reach this bar.** Complete the declarative widget script binding + evidence handles, the dependency graph with safe-delete, and definition immutability --- gate G3. Acceptance: the full demo builds from empty through the HMI only, with no hardcoded page or widget. |
+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**13 · The Zero-Defect Quality Bar**

The North Star sensation "it won\'t embarrass me" is enforced, not hoped for. The bar is enumerated and proof-backed: a feature is done when its three contracts (§3) are all green, and no gate item is allowed to sit at "85%."

  --------------------------- -------------------------------------------------------------------------------------------------------------
  **Quality dimension**       **The bar**

  **No dead buttons**         Every control acts or is disabled with a visible reason (enumerated action matrix).

  **No silent failure**       Every error is typed and specific, with the next safe step; every slow call shows progress.

  **No fabricated numbers**   Every surfaced number carries a resolvable evidence handle; the assistant cannot render one it cannot cite.

  **No facade**               Seeded demo rows are tagged origin = seed; learning jobs recompute on demo data.

  **No cross-tenant leak**    Cross-tenant access returns 403/empty everywhere --- an explicit test.

  **No unhonest claim**       Forbidden-language lint passes (§2, Appendix E).
  --------------------------- -------------------------------------------------------------------------------------------------------------

+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The no-"85%" rule**                                                                                                                                                                                                                                                                              |
|                                                                                                                                                                                                                                                                                                    |
| Critical gate items require 95--100% completion, never an average of 85%. A demo is not "mostly working"; either every button on the path works, or the path is not demo-ready. This is the discipline that lets an unscripted live demo survive a skeptical engineer clicking wherever they like. |
+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**14 · Demo doctrine & the Golden Rules**

The demo is the product\'s first proof. The compass set one rule above all others, and v7 preserves it verbatim because it is the discipline that keeps the product honest with itself.

+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The Golden Rule --- preserved from the compass, word for word in spirit**                                                                                                                                                                                                                        |
|                                                                                                                                                                                                                                                                                                    |
| **The demo is not a separate app or an extra layer.** It is the first real release --- the trial version --- running on a prepared dataset.                                                                                                                                                        |
|                                                                                                                                                                                                                                                                                                    |
| **Nothing is hardcoded.** No hardcoded widgets, no hardcoded pages. Every DB Link, every Job, every Page, every widget, and every SQL view is configured from the HMI front end --- the same way a real customer would.                                                                            |
|                                                                                                                                                                                                                                                                                                    |
| **Why this rule exists.** Building the demo through the HMI means experiencing the product as a real user does, which is the only way to find all the real bugs. "If we hard code some pages we will fake our self" --- and a faked demo proves nothing and hides the defects a customer will hit. |
+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

![Figure 9 · The flat-steel demo plant --- eight real source systems feeding one canonical model, built entirely through the HMI.](media/634c2b2a9a1991bf1c3fcb99a980918cfc257150.png "Figure 9 · The flat-steel demo plant — eight real source systems feeding one canonical model, built entirely through the HMI."){width="6.25in" height="3.1875in"}

*Figure 9 · The flat-steel demo plant --- eight real source systems feeding one canonical model, built entirely through the HMI.*

**14.1 · The eight demo sources**

The demo models a realistic flat-steel plant across thirteen areas (EAF, LF, continuous caster, transfer, hot-strip mill, skin-pass, two cooling/coil lines, slitting, pickling, galvanizing, yards, roll shop), fed by eight source systems with realistic, imperfect data. The full table-level blueprint is Appendix A; the eight sources are: MeltShop (PostgreSQL), Caster (Oracle), HSM (Oracle), PKL (MSSQL), Yard (Excel), Downtime (MySQL), Surface Inspection / Parsytec (MySQL), and QA (Excel).

**14.2 · Scale & deliberate imperfection**

  ----------------------------------- --------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Dimension**                       **Demo figure**

  **Heats / month · coils / month**   ≈ 630 heats · ≈ 5,600 coils

  **Heat / coil mass**                ≈ 160 t heat · ≈ 18 t coil

  **Caster**                          3.6--5.6 m/min · 4 LF stations · 11 ladles

  **QA sampling**                     every 3rd coil

  **Deliberate imperfection**         missing heat links, ambiguous keys, transition coils, uninspected coils, false detections --- so the demo proves the honesty mechanisms, not a clean fixture
  ----------------------------------- --------------------------------------------------------------------------------------------------------------------------------------------------------------

**14.3 · From demo to pilot**

The demo proves the engine; a Proof-of-Value pilot proves the signal on the customer\'s own data. The demo is the trial version on prepared data --- the same release a pilot customer receives, pointed at their sources. This is why the "Trial" tier (§9) and the demo are the same artifact, not two.

+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The anti-facade rule, restated for the demo**                                                                                                                                                                                                                            |
|                                                                                                                                                                                                                                                                            |
| Seeded rows are tagged origin = seed and never presented as live computation; learning jobs recompute on demo data so what the buyer sees is the engine running, not a screenshot. A demo built through the HMI is also the most honest acceptance test of gate G3 and G7. |
+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §14 (Wave C/D: demo readiness)**                                                                                                                                                                                                                                                                                                                                                    |
|                                                                                                                                                                                                                                                                                                                                                                                                      |
| **Today.** A demo-reset job foundation and seed tooling exist; the full eight-source dataset and one-click readiness check are not yet complete. **To reach this bar.** Author the eight source datasets per Appendix A at realistic scale and imperfection, build the demo from empty entirely through the HMI, and add the one-click readiness check + recorded clean dry run --- gates G3 and G7. |
+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**15 · Website, brand & product ecosystem**

The website explains the product, the ecosystem, pricing, proof and security, and captures a lead --- with no unsupported claim. It carries the one brand identity (Appendix C) across app, website and reports.

**15.1 · The product ecosystem --- core + packs**

PlantProcess IQ is the core platform. Rather than five separate products, the company offering is the PPIQ core plus optional capability packs that extend it for specific plant functions --- so a customer buys one platform and adds what their plant needs.

  --------------------------------- -----------------------------------------------------------------------------------------------------------
  **Offering**                      **What it is**

  **PlantProcess IQ (core)**        The read-only process-to-quality intelligence platform --- connect, map, analyse, value, suggest, assist.

  **Quality / Surface pack**        Deeper defect-driver workflows, defect-catalog harmonization, claim analysis.

  **Energy pack**                   Energy-KPI intelligence and per-MWh impact in the value model.

  **Yard / Logistics pack**         Inventory, coil location, buffer and stock-age intelligence.

  **Reliability / Downtime pack**   Production-impact downtime modelling and cascade analysis.
  --------------------------------- -----------------------------------------------------------------------------------------------------------

+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **An honest evolution from the compass\'s five products**                                                                                                                                                                                                                                                                                                                                 |
|                                                                                                                                                                                                                                                                                                                                                                                           |
| The compass named five sibling products (PPIQ, MES, QES, Yard, Energy). v7 states the deliberate evolution plainly: the MES/QES ambition is not a separate execution system --- it is delivered as capability packs on the read-only core, which keeps the honesty contract (PPIQ never executes) intact. §23 records this pivot so the traceability is honest, not silently re-labelled. |
+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**16 · Deployment, operations, scale & SLA**

**16.1 · Four topologies, one codebase**

  --------------------- -------------------------- --------------------------------------------------------------------
  **Topology**          **Isolation**              **For**

  **SOU-hosted SaaS**   Logical (TenantId + RLS)   Most plants; fastest onboarding

  **Customer cloud**    Logical or dedicated       Plants with a cloud mandate

  **On-prem**           Physical (single tenant)   Plants that keep data inside their walls

  **Air-gapped**        Physical, offline          High-security plants; perpetual licence, signed offline activation
  --------------------- -------------------------- --------------------------------------------------------------------

**16.2--16.3 · Sizing & time-series scale**

Storage is split by shape: relational source copies in PostgreSQL; high-frequency process parameters in a time-series-optimized store (hypertables / columnar partitioning, compression, downsampling, retention), because process parameters are billions of rows and a naive row table in vanilla Postgres does not scale. A reference footprint is published per plant size.

**16.4 · SLA, DR/HA, escrow, air-gapped update**

SLA-backed support at Enterprise; documented backup/restore with a verified restore drill; health and readiness endpoints; incident runbooks tested so a clean machine reaches a working login by runbook alone; defined RPO/RTO for HA/DR; source-code escrow for on-prem customers; and a signed, offline update bundle for air-gapped sites (including a validated model bundle, §7.9).

+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §16 (Wave D: ops & scale)**                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| **Today.** A live production deployment runs on a Hetzner Ubuntu host via Docker Compose (eight containers, wildcard DNS, idempotent SQL), with CI/CD via Jenkins + GitHub webhooks. Outstanding: the publicly-exposed PostgreSQL port must be bound to 127.0.0.1, and the bootstrap admin account replaced. **To reach this bar.** Close the exposed port and rotate the bootstrap admin (Wave A security); add the time-series store, backup/restore drill, health/readiness endpoints and runbooks --- gate G9; document the per-size sizing footprint. |
+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**17 · The ten operating principles**

  -------- ---------------------------------------------------------------------------------------------------------------------------
  **\#**   **Principle**

  **1**    If it cannot be configured from the HMI, it is not done. No hardcoding --- including the demo and the assistant.

  **2**    Deterministic engines compute; the assistant explains with citations. The model never does arithmetic or ranks a finding.

  **3**    Every surfaced claim carries a resolvable evidence handle, or it is not rendered.

  **4**    Suspected contributor, never guaranteed root cause. Restraint under statistical discipline is a feature.

  **5**    Read-only is absolute. No setpoint, recipe or command ever flows back to a control system.

  **6**    State the population. No silent survivorship; coverage is shown on every analysis.

  **7**    Fail loudly and specifically. A typed error with a next step beats a silent wrong number.

  **8**    One codebase, four topologies. Logical isolation in SaaS, physical when dedicated --- no forks.

  **9**    The demo proves the engine; the pilot proves the signal on the customer\'s own data.

  **10**   Honesty is the strongest sales asset. The boundary is the product\'s integrity, not a disclaimer.
  -------- ---------------------------------------------------------------------------------------------------------------------------

+--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **17.B · In plain terms --- why these protect the buyer**                                                                                                                                                                                                                                                          |
|                                                                                                                                                                                                                                                                                                                    |
| Together these ten are why a skeptical engineer can trust the tool: nothing is faked, every number is traceable, the language never over-claims, the architecture never touches control, and the product tells you what it does not know. They are the spine that has held from the compass through every version. |
+--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**18 · Security, privacy & compliance**

A serious procurement checks these explicitly. v7 commits the full posture and ties each item to a gate.

  -------------------------------- -----------------------------------------------------------------------------------------------------------------------
  **Area**                         **Commitment**

  **Secure SDLC**                  Code review, dependency and secret scanning, SAST/DAST, signed builds, least-privilege service accounts.

  **Encryption**                   TLS in transit; encryption at rest for staging, canonical and the credential vault; secrets never in the browser.

  **Identity**                     §10.2 --- Argon2id, MFA, SSO/SCIM, session policy, the localStorage token retired.

  **Tenant isolation**             TenantId + RLS in SaaS; physical isolation when dedicated; cross-tenant access tested to return 403/empty.

  **Audit**                        Every protected action, job and assistant call is audited (who, what, when, what classification left a boundary).

  **Certifications (direction)**   SOC 2 Type II and ISO 27001 as the compliance roadmap; GDPR by design.

  **Regulated industries**         A GxP / 21 CFR Part 11 pack (audit trails, e-signature, validation evidence) for pharma and regulated process plants.
  -------------------------------- -----------------------------------------------------------------------------------------------------------------------

+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Build note --- §18 (Wave A first, Wave D for certification)**                                                                                                                                                                                                                                                                                                                                                                    |
|                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| **Today.** Prior audits flagged a hardcoded JWT signing key, unconditional dev-seed endpoints in production, dual token storage, and missing indexes. **To reach this bar.** Wave A closes these: per-environment signed keys, dev-seed endpoints gated out of production, the localStorage token retired, encryption at rest, and the audit log --- gate G12. SOC 2 / ISO 27001 and the GxP pack are the Wave D compliance track. |
+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**19 · Value, ROI & competitive positioning**

PPIQ is priced as a plant platform because it pays back as one. The value engine (§7.5) makes the case on the customer\'s own pilot data, with every input drill-throughable.

![Figure 7 · The value loop --- fragmented data becomes understanding, euro-ranked priorities, and proof that acting paid.](media/e473013ef868eac1fa42cd7c21ca9273017f0bfa.png "Figure 7 · The value loop — fragmented data becomes understanding, euro-ranked priorities, and proof that acting paid."){width="6.25in" height="3.1354166666666665in"}

*Figure 7 · The value loop --- fragmented data becomes understanding, euro-ranked priorities, and proof that acting paid.*

**19.1 · The payback case (worked, on the demo plant)**

  ------------------------------------------------------------- ---------------------------------------------------------------------
  **Step**                                                      **Figure**

  **One worked finding (edge-crack / superheat, S355)**         €28k--€56k / month, point ≈ €42k (§7.5)

  **A mature plant surfaces several such findings**             Annualized opportunity ≈ €340k--€670k (illustrative, on demo scale)

  **Realistic capture (a fraction of opportunity, acted on)**   \~25% capture → comfortably exceeds platform cost

  **What the buyer signs against**                              Payback on their own pilot data --- not a synthetic promise
  ------------------------------------------------------------- ---------------------------------------------------------------------

**19.2 · Where PPIQ wins**

  -------------------------------------- --------------------------------------------------------------------------------------------------------------------------------------------
  **Versus**                             **PPIQ\'s edge**

  **Generic BI (Power BI / Qlik)**       Carries manufacturing semantics --- genealogy, defect-drivers, value, suggestions --- instead of charts on data you must prepare.

  **A bespoke data-science project**     A configurable product, not a one-plant build; mappings, pages and KPIs are authored from the HMI and reused across plants.

  **A black-box AI tool**                Transparent math, named methods, evidence handles, and an assistant that refuses to guess --- what a skeptical engineer needs to trust it.

  **A control / MES vendor\'s add-on**   Read-only and vendor-neutral --- it connects around existing systems rather than locking the plant to one stack.
  -------------------------------------- --------------------------------------------------------------------------------------------------------------------------------------------

+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **19.B · In plain terms --- the one-line case**                                                                                                                                                                                                      |
|                                                                                                                                                                                                                                                      |
| PPIQ turns the data your plant already produces into euro-ranked, evidence-backed priorities --- without touching control, without a remodel, and without asking you to trust a black box. The demo proves the machine; your pilot proves the money. |
+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**PART II**

**Realization --- how to get from today\'s build to this bar**

Part I is the destination. Part II is the honest map: where the code is now (\~46/100), the four waves that close the distance, the capability-by-capability delta, the acceptance gates that define "done," and the proof that every point still traces back to the V1 compass. A large gap between this doctrine and the code is acceptable --- because the way across it is written down here.

**20 · The realization roadmap --- REDIRECTED (v8.1)**

*(v8.1, per directive 3: two living roadmaps in two documents is how plans fork - and it happened: the four waves below were superseded by the M1-M4 milestone plan. The v8 wave text and Appendix I are removed; their content survives in the archive copy of v8.)*

The realization plan lives in exactly one place: **PPIQ_Product_Roadmap_v9.md**, executed through **PPIQ_Product_Backlog_v23.xlsx** (frozen ID namespace, Origin column mandatory). This specification states the destination; the roadmap owns the route. Where a build-status claim is needed inside this document, it names its evidence (a gate, a report file, an audit) or is written in the future tense.

**21 · The doctrine-vs-build delta**

**RETIRED AS A SNAPSHOT (v8.1):** the table below is frozen exactly as written on 26-Jun-2026 and is NO LONGER MAINTAINED - a hand-maintained delta table inside a specification is guaranteed to lie (it already did: "Access token in localStorage" was falsified by gate P2-T05, connector rows contradicted §4.7, genealogy and L4 rows were overtaken by 15-Jul evidence). Build-vs-doctrine deltas are owned by the Implementation Audit lineage (12-Jul audit; next post-demo). Read what follows as history, not status.

  ----------------------------------- ------------------------------------------------------------- -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- ---------- ----------
  **Capability**                      **Doctrine target (Part I)**                                  **Current build reality**                                                                                                                                                                                                                                **Gate**   **Wave**

  **Access token**                    In-memory + HttpOnly refresh cookie (§10.2)                   Stored in browser localStorage                                                                                                                                                                                                                           G12        A

  **Signing keys / dev-seed**         Per-env signed keys; dev-seed gated out of prod               Per-env signing key enforced (≥64 chars; dev/default keys rejected in prod); dev license key demo-gated via PPIQ_PRESENTATION (26 Jun 2026). Open: real production Ed25519 LICENSE signing keypair (Option 3)                                            G12        A

  **Exposed services**                DB bound to 127.0.0.1; no bootstrap admin                     Bootstrap admin replaced by permanent sysadmin owner, auto-provisioned at first run (customer admin is a separate manual commissioning step); test users removed from the production seed path (26 Jun 2026). Verify server DB-port binding              G9/G12     A

  **Value Engine**                    Bounded euro ranges, abstain path (§7.5)                      Entirely unstarted                                                                                                                                                                                                                                       G5         B

  **L4 statistics**                   Spearman, MI, Lasso, VIF, bootstrap + FDR (§7.3)              Unstarted (ML-readiness only)                                                                                                                                                                                                                            G5         B

  **Suggestion / assistant guard**    Deterministic suggestions; no-fabrication guard (§7.6--7.7)   Partial / unstarted                                                                                                                                                                                                                                      G4         B

  **Edge Collector**                  One-way push + source-impact budget (§4.6)                    Unstarted                                                                                                                                                                                                                                                G11        C

  **Connectors**                      Relational + one historian GA, honest status (§4.7)           CSV/Excel + MSSQL/MySQL; SafeSqlValidator passing                                                                                                                                                                                                        G1         C

  **Schema drift / mapping health**   Snapshot + typed pause + health panel (§4.9)                  Unstarted                                                                                                                                                                                                                                                G2         C

  **Genealogy / coverage**            Recursive spine + coverage panel + blended provenance (§5)    Partial                                                                                                                                                                                                                                                  G2         C

  **Demo-through-HMI**                Full demo built from empty via HMI (§14)                      Deploy provisions a clean stack + sysadmin identity + Enterprise license + loginable UI end-to-end (26 Jun 2026); full demo-from-empty-through-HMI remains the Wave-C target on this substrate                                                           G3/G7      C

  **Accessibility / i18n**            WCAG AA light theme + Arabic RTL (§8.6--8.7)                  Dark theme; EN only                                                                                                                                                                                                                                      G14        D

  **Ops & scale**                     Time-series store, restore drill, runbooks (§16)              Single Docker host; CI/CD GREEN end-to-end (build/test/migrate/seed/deploy-in-place/health-gate+rollback/presentation smoke), two-project split (ppiq-app app / plantprocessiq infra) --- 26 Jun 2026; time-series store + restore drill remain Wave-D   G9         D

  **Compliance**                      SOC 2 / ISO 27001 + GxP pack (§18)                            Roadmap only                                                                                                                                                                                                                                             G16        D
  ----------------------------------- ------------------------------------------------------------- -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- ---------- ----------

+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Reading the delta honestly**                                                                                                                                                                                                                                                                                                                                                                                        |
|                                                                                                                                                                                                                                                                                                                                                                                                                       |
| The build is strong where it has been worked --- connectors, the SafeSqlValidator, the admin shell, a live deployment and CI/CD --- and unstarted where the hardest value lives --- the value engine, L4 statistics, the Edge Collector and demo-through-HMI. That is exactly the shape Part II is built to close, in the order that makes the product safe (A), valuable (B), provable (C) and enterprise-ready (D). |
+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**21.1 · v8 Delta Addendum --- what moved on 26 Jun 2026 (deploy & identity)**

Per the discipline in §20.2 ("freeze the doctrine; move the delta"), Part I is unchanged; this addendum records the delta that moved. A focused session drove the server deploy pipeline to GREEN end-to-end and made the demo UI loginable. This closed a slice of Wave A and advanced the substrate beneath Wave C, but did NOT move the headline persona score, which is gated on the value engine and live HMI signal (Wave B), still unbuilt. A green deploy pipeline is infrastructure, not product value; the baseline moved from ≈ 46/100 to ≈ 50--52/100, ceiling unchanged at ≈ 84/100.

Closed this session (Wave A slice): per-environment signing keys enforced (≥64 chars; dev/default keys rejected in Production); the dev license key is now registered only in demo mode (PPIQ_PRESENTATION=on) and never in a real customer Production; the bootstrap admin is replaced by a permanent, undeletable sysadmin owner auto-provisioned at first run, with customer/tenant admins added manually at commissioning (the two-admin rule); test users were removed from the production seed path; CI/CD is GREEN end-to-end (pull, backend tests, frontend tests, app-DB migrate, seed, build, recreate the canonical stack in place, health gate with rollback to :previous, presentation smoke proving sysadmin login + Enterprise activation); and the demo frontend is wired to the public host generically (one PPIQ_SITE_HOST drives every browser URL and CORS origin).

Server topology decision (canonical): two Docker Compose projects that must never be merged --- the application project ppiq-app (postgres, api, web) deployed by the pipeline, and the infrastructure project plantprocessiq (Jenkins, Caddy, backup-runner) which is sacred and must never be reaped by an app deploy. Merging them previously caused docker compose \--remove-orphans to kill Jenkins mid-deploy; the rename to ppiq-app is the permanent fix. A single fronting Caddy serves both over HTTPS. Full operational detail lives in the Identity and Topology v4 reference and the Deploy Pipeline Handover.

Still open (tracked): generate a real production Ed25519 license signing keypair and sign per-customer/per-tier tokens (Option 1 to Option 3) so the dev key never ships to a real customer, and ensure real customer frontends do not bake demo credentials; re-establish a persistent, host-bound Caddyfile with corrected route targets; wrap first-run provisioning in try/catch; preserve the Postgres password across env regeneration; and move Jenkins to a separate Docker network. Decision-record note: install-time identity provisions only the permanent sysadmin support account; customer admins are added at commissioning --- nothing customer-usable is baked into the install image.

**22 · Acceptance gates & definition of done**

The next backlog is generated from this section. Each task maps to one gate, one validation method and one evidence artifact --- with a measurable exit, so "done" is provable, not asserted. v7 carries v4\'s ten gates forward and adds the six the hardened spec requires (G11--G16).

  ------------------------------------ ----------------------------------------------------------------------------------------------------------------------------- --------------------------------------------------------------------------------------------------------------------------
  **Gate**                             **Definition of done**                                                                                                        **Measurable exit**

  **G1 Source integration**            Each connector: honest availability, test-before-save, masked secrets, staging, import batch, failure tests.                  Connectors pass behavioural tests; credentials never appear on read-back.

  **G2 Mapping & genealogy**           Business keys, canonical mapping, safe SQL, validation, versioning, genealogy, rollback on demo data.                         A coil resolves both genealogy directions; a bad mapping returns a typed error and rolls back.

  **G3 Workflow (HMI)**                Configure data, build page, configure widgets, run jobs, view dashboards, generate reports --- from the HMI, no hardcoding.   A dry run builds the full demo from empty via HMI only; no hardcoded page/widget used.

  **G4 Intelligence**                  Simple + advanced analysis, value, suggestions, assistant produce evidence-linked, honest, tenant-safe outputs.               Golden dataset recovers true signals + rejects spurious under FDR; assistant emits no uncited number.

  **G5 Access & value**                RBAC, license, tenant isolation, audit on every endpoint/page/job/tool; the value engine computes a bounded range.            Authorization matrix green by identity + tier; cross-tenant returns 403/empty; the €28k--€56k range reproduces.

  **G6 UI/UX**                         Design system, edge states, no-dead-button on every page/action.                                                              Action matrix fully green by enumeration; visual-regression + cross-browser pass.

  **G7 Demo**                          Eight-source, thirteen-area, realistic dataset; reset + rebuilt through product workflows.                                    One-click readiness check passes; recorded clean dry run exists.

  **G8 Website**                       Ecosystem, pricing, proof, security, CTAs, brand --- no unsupported claims.                                                   Honesty-lint passes; CTA captures a lead; brand audit matches Appendix C.

  **G9 Operations**                    Deploy, backup, restore, health, readiness, logging, incident runbooks tested.                                                A clean machine reaches a working login by runbook only; restore verified.

  **G10 Quality bar**                  Every §13 row has passing automated or documented proof.                                                                      All gate rows green; no "85%" on a gate item.

  **G11 OT-safe acquisition**          Edge Collector with one-way push, source-impact budget, CDC/replica preference.                                               Collector pushes one-way; no inbound path to OT; source-load stays within budget.

  **G12 Identity & security**          Argon2id, MFA for admins, in-memory token + HttpOnly refresh, SSO/SCIM, per-env keys, dev-seed gated.                         localStorage token retired; admin MFA enforced; dev-seed absent in prod build.

  **G13 Data boundary & model gov.**   Model gateway with three serving modes; data-minimization; redaction; per-tenant no-egress toggle; eval harness.              Self-hosted mode leaks nothing; private-endpoint sends only question + scoped evidence; harness gate blocks regressions.

  **G14 Accessibility & i18n**         WCAG 2.1 AA, contrast-audited light theme, RTL, locale/units/timezone.                                                        AA audit passes; Arabic RTL renders; units/timezone switch per user.

  **G15 Coverage & honesty**           Population stated on every analysis; blended provenance; label-quality surfaced.                                              Every finding shows its population and exclusions; a transition coil reports weighted attribution.

  **G16 Compliance**                   Secure SDLC evidence; SOC 2 / ISO 27001 track; GDPR; GxP / 21 CFR Part 11 pack.                                               SDLC controls evidenced; GxP audit-trail + e-signature available where required.
  ------------------------------------ ----------------------------------------------------------------------------------------------------------------------------- --------------------------------------------------------------------------------------------------------------------------

+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **Task-list generation rule**                                                                                                                                                                                                                                                                                                                                                                                           |
|                                                                                                                                                                                                                                                                                                                                                                                                                         |
| The backlog Excel must not merely list tasks. Each row carries: Target Section, Acceptance Gate, Existing Foundation, Implementation Description, Validation Command, Evidence Artifact, Severity, Dependency, Owner Role, Estimated Hours, and Minimum Completion Threshold --- with critical gates requiring 95--100%, never an average of 85%. Appendix I maps the waves to these gates and the eleven build phases. |
+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**23 · Traceability --- the V1 four-track skeleton to v7**

Everything in v7 still has a home in the compass. The V1 4-Track Vision named four tracks; v7 deepens each and --- honestly --- names the one place the product\'s shape evolved away from the original brain-dump.

  -------------------------------------- ------------------------------------------------------------------------------ -----------------------------------------------------------------------------------------------------------
  **V1 track (the compass)**             **Where it lives in v7**                                                       **Evolution (named honestly)**

  **Track 1 --- Workflow / product**     §1--§12 (the journey, mapping engine, intelligence, widgets) + Appendix A--B   Deepened with worked SQL, the value model and the canonical spine; unchanged in intent.

  **Track 2 --- Hardening**              §13, §16, §18, and all of Part II                                              Expanded into an enumerated quality bar, security posture and a realization roadmap; unchanged in intent.

  **Track 3 --- Demo**                   §14 + Appendix A                                                               Golden Rules preserved verbatim; the buildable eight-source blueprint is recovered in Appendix A.

  **Track 4 --- Website / commercial**   §9, §15, §19 + Appendix F, H                                                   Deepened with signed licensing, ROI and competitive positioning.
  -------------------------------------- ------------------------------------------------------------------------------ -----------------------------------------------------------------------------------------------------------

+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The one honest pivot --- recorded, not hidden**                                                                                                                                                                                                                                                                                                                                                                                                                                            |
|                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| **Five products → core + packs.** The compass imagined five sibling products (PPIQ, MES, QES, Yard, Energy). v7 delivers them as the read-only PPIQ core plus capability packs (§15.1), not separate execution systems --- because a MES/QES that executes would break the honesty contract (§2) that is the product\'s integrity. This is the single deliberate evolution from the compass, and naming it here is what gives v7 the internal consistency the lineage audit found v6 lacked. |
+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

+--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+
| **The spine that never moved**                                                                                                                                                                                                                                                                                                                   |
|                                                                                                                                                                                                                                                                                                                                                  |
| Across the compass and every version, four commitments never changed: the honesty contract (suspected contributor, never root cause), generic-not-bespoke architecture, the demo built entirely through the HMI, and no fabricated numbers. v7 is the most complete expression of that spine, with the route to build it written down beside it. |
+--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

**APPENDICES**

**Reference --- the buildable detail, the brand, and the answers**

A recovers the table-level Demo Data Blueprint (melt shop restored) the lineage thinned. B--E are the engineering and brand references. F is the buyer objection playbook. G--I carry deployment sizing, competitive positioning and the build-backlog mapping.

**Appendix A · Source-System Blueprint for the demo data**

The buildable detail v5 compressed, recovered in full --- including the EAF/Ladle melt shop as a first-class source. This is the table-level specification the demo dataset is generated from (the Golden Rule, §14: built and loaded through the HMI, never hardcoded).

  ------------------------------------------- ------------------------------------------------------------------------------------------------------------------------
  **Source**                                  **Representative tables / objects to model**

  **MeltShop (PostgreSQL)**                   equipment_counter, heats, heat_steps, heat_parameters, samples, sample_results, components, additives, ladle_usage

  **Caster (Oracle)**                         heat, sequence, slab, strand, mould, tundish, caster_samples, caster_sample_results, casting_parameters

  **HSM (Oracle)**                            slab_input, coil_output, pass_schedule, rolling_parameters, temperature_profile, width_thickness_profile, coil_summary

  **PKL (MSSQL)**                             coil_route, pickling_process_events, line_parameters, quality_holds, operation_summary

  **Yard (Excel)**                            yard_inventory, coil_location, movement_history, buffer_status, stock_age

  **Downtime (MySQL)**                        downtime_events, reason_codes, equipment_stoppage, production_stoppage, shift_crew, maintenance_notes

  **Surface Inspection / Parsytec (MySQL)**   inspection_runs, defect_events, defect_catalog, defect_positions, severity, coil_surface_summary

  **QA (Excel)**                              qa_samples, lab_results, component_catalog, customer_priority_samples, quality_decisions
  ------------------------------------------- ------------------------------------------------------------------------------------------------------------------------

Realism parameters (from the compass) the dataset must honour: ≈ 630 heats/month and ≈ 5,600 coils/month; ≈ 160 t per heat, ≈ 18 t per coil; caster 3.6--5.6 m/min, 4 LF stations, 11 ladles; QA sampling on every 3rd coil; thirteen plant areas (EAF, LF, CC, TF, HSM, SKP, two cooling/coil lines, slitting, PKL, GVL, yards, roll shop); and deliberate imperfection --- missing heat links, ambiguous keys, transition coils, uninspected coils, false detections --- so the demo proves the honesty mechanisms (§5.3--5.5), not a clean fixture.

**Appendix B · Worked mapping, genealogy & blended-provenance reference**

The full artifact behind §4.3--4.4 and §5.1--5.3, the spine a buyer\'s technical reviewer can read end to end: the business-key dictionary (CoilId, §4.3), the canonical join view canon.quality_event (§4.3), the KPI view canon.kpi_first_pass_surface_yield (§4.4), the traced thread (Coil C-0044170 → Slab S-44170-A → Sequence SEQ-44 → Heat H-3361, forward to 5 edge-crack defects, §5.1), and the blended-provenance weighting for transition material (§5.3). See the dark code panels in §4.3, §4.4 and §12.1 and the genealogy table in §5.1.

**Appendix C · Brand token reference**

The canonical swatch set (exact hex, from the compass). One identity across app, website and reports --- the "Dark Industrial Command Center."

  --------------------- --------------- -------------------------------------
  **Name**              **Hex**         **Application**

  **Deep Navy Black**   #050B18         App + website background

  **Panel Navy**        #0B1730         Sidebar, cards, modals

  **Industrial Blue**   #102A43         Table headers, nav, dividers

  **Electric Blue**     #0A84FF         Primary CTA, active state

  **Electric Cyan**     #00D4FF         Glow, chart primary, hover

  **Cyan Green**        #2CE6A2         Success, OK, Enterprise badge, gain

  **Amber**             #FFB020         Warning, watch, Pro badge

  **Hot Red**           #FF4D6D         Critical, failed, drift

  **Near White**        #EAF6FF         Text on dark

  **Muted Steel**       #8EA7C1         Secondary text

  **Light Grey**        #F4F6F8         PDF / report surface
  --------------------- --------------- -------------------------------------

Type: Inter (UI / web) · JetBrains Mono (SQL / code) · min 14 px web / 12 pt PDF · web scale 48 / 36 / 28 / 20 / 16 · app scale 24 / 18 / 14 / 12.

**Appendix D · Glossary**

  ------------------------------- ------------------------------------------------------------------------------------------------
  **Term**                        **Meaning**

  **Source-shaped staging**       A copy of source data kept in its original structure before canonical mapping.

  **Canonical model**             The generic PPIQ data model used for cross-plant analytics and dashboards.

  **Business key**                A real-world identifier (HeatId, CoilId) used to link source systems.

  **Genealogy spine**             The parent-child material path from heat to slab to coil to downstream units.

  **Provenance handle**           An evidence reference attached to any surfaced claim (finding / job / dataset / source / doc).

  **Blended provenance**          Weighted multi-heat attribution for transition (casting-boundary) material.

  **Abstain**                     The value engine\'s explicit "insufficient basis" output when an assumption is missing.

  **Production-impact minutes**   Output actually lost, after buffers and cascades --- distinct from equipment-stopped minutes.

  **Readiness gate**              The data-sufficiency check that turns an advanced analysis Ready / Partial / Blocked.

  **Zero-Defect Quality Bar**     The enumerated, proof-backed standard that the app is demo- and buyer-review-safe.
  ------------------------------- ------------------------------------------------------------------------------------------------

**Appendix E · Approved & forbidden commercial claims**

  ------------------------------------------------------------------- ----------------------------------------------------------
  **Approved now**                                                    **Forbidden unless truly proven**

  **Read-only manufacturing intelligence layer**                      We replace MES, SCADA, PLC, Level-2 or BI

  **Correlation analysis + suspected-contributor ranking**            Guaranteed root cause

  **Rule-based risk scoring + ML-readiness foundations**              Production-ready AI prediction

  **Evidence-based investigation + recommendation support**           Autonomous optimization / automatic plant control

  **Generic architecture for multiple manufacturing industries**      Steel-only product or one-plant hardcoded solution

  **Offline / on-prem / air-gapped AI as enterprise direction**       Live enterprise AI capabilities without deployment proof

  **Connector capability & availability shown honestly per source**   Live Oracle / historian ready today (unless certified)
  ------------------------------------------------------------------- ----------------------------------------------------------

**Appendix F · Buyer objection playbook**

The hard questions a skeptical plant manager, IT/OT lead, metallurgist or procurement officer asks --- with the honest, doctrine-grounded answer. Every answer points to where it is committed in Part I.

  --------------------------------------------------------------------- -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
  **Objection / question**                                              **Honest answer (and where it lives)**

  **"Will this touch my control systems?"**                             No. PPIQ is read-only; no setpoint, recipe or command ever flows back (§2, §2.1). That is why your automation team can approve it without a control-systems risk review.

  **"How do you reach our data without burdening the historian?"**      A customer-controlled Edge Collector in the DMZ, preferring CDC or a read replica over polling the primary, with a signed-off source-impact budget (§4.6).

  **"We won\'t allow inbound connections to OT."**                      The collector pushes one-way; the core never initiates a connection into OT, and a data-diode option exists for the strictest plants (§4.6).

  **"Our coil IDs don\'t match across systems."**                       Expected. The business-key dictionary normalizes and reconciles them explicitly (e.g. C-0044170 ≡ 44170), and conflicts are rejected, never silently merged (§4.3).

  **"Is this just another BI dashboard?"**                              No. It carries manufacturing semantics --- genealogy, defect-drivers, value, suggestions --- not charts on data you must prepare (§19.2).

  **"Do I need a SQL expert to configure it?"**                         For the common case, no --- a no-code visual mapper plus templates; safe-SQL is only for the long tail (§4.5).

  **"How much data do you need before it\'s useful?"**                  Simple dashboards and KPIs work on day one; advanced findings arrive as readiness gates turn Ready, and a history backfill collapses that timeline (§6.3, §4.8).

  **"Is the AI a black box?"**                                          No. Deterministic engines compute and rank; the assistant only explains with citations and cannot render a number it cannot cite (§7, §7.7).

  **"Can it guarantee the root cause?"**                                No, and it never claims to --- it surfaces evidence-ranked suspected contributors with the method shown (§2, §7.4). That honesty is the point.

  **"What about a lurking variable you don\'t measure?"**               Findings are framed as suspected contributors; PPIQ stratifies by every visible confounder and says plainly when a likely one is unmeasured (§7.11).

  **"Your yield won\'t match our MES."**                                Probably not, because definitions differ. KPI definitions are versioned and transparent, and PPIQ reconciles to your authoritative definition and explains the gap (§7.11).

  **"A transition coil came from two heats --- which one?"**            Both. PPIQ models blended provenance with weights and never fabricates a clean single-heat answer (§5.3).

  **"Where does our data go for the AI?"**                              Nowhere to be computed --- engines run in-tenant. The assistant LLM is self-hosted by default; SaaS uses a zero-retention private endpoint receiving only the question and scoped evidence; or bring your own model (§7.8).

  **"Can we run it fully offline / air-gapped?"**                       Yes --- one codebase, physical isolation, a self-hosted model, and a cryptographically signed licence verified offline (§16.1, §9.4).

  **"How is it licensed, and can someone flip their own tier?"**        Entitlements live in an Ed25519-signed token verified by the app, not an editable DB row --- so an on-prem customer cannot UPDATE their tier (§9.4).

  **"What happens when our licence expires?"**                          Clear warnings, then a configurable grace period (read-only), then read-only of existing dashboards --- your data is never destroyed (§9.3).

  **"How do you handle our SSO and user provisioning?"**                SAML 2.0 / OIDC for SSO and SCIM 2.0 for automatic provisioning and deprovisioning, with group-to-role mapping (§10.2).

  **"What about accessibility and Arabic / German?"**                   WCAG 2.1 AA with a contrast-audited light theme, and first-class English, German and Arabic including right-to-left layout (§8.6--8.7).

  **"Two of my engineers edit the same dashboard --- what happens?"**   Optimistic concurrency with a clear conflict dialog, live presence, and immutable published versions --- no one silently loses work (§8.9).

  **"Is the demo real or smoke and mirrors?"**                          Real. It is the trial release on a prepared dataset, built entirely through the HMI; seeded rows are tagged and learning jobs recompute live (§14).

  **"Prove it pays for itself."**                                       A worked business case on your own pilot data, with payback and every input drill-throughable --- e.g. €28k--€56k/month on one edge-crack finding (§7.5, §19.1).

  **"What if our source schema changes after go-live?"**                Drift is detected, dependent mappings are paused with a typed reason, and a Mapping Health panel shows what broke and why --- never a silent wrong number (§4.9).

  **"Is your security real or aspirational?"**                          Argon2id hashing, MFA for admins, in-memory tokens with HttpOnly refresh, tenant isolation tested to 403/empty, full audit, and a SOC 2 / ISO 27001 track (§10.2, §18).

  **"We\'re regulated (pharma)."**                                      A GxP / 21 CFR Part 11 pack provides audit trails, e-signature and validation evidence (§18).

  **"What is honestly not built yet?"**                                 See §21. The strongest unbuilt items --- the value engine, L4 statistics, the Edge Collector and demo-through-HMI --- are scheduled in Waves B and C with acceptance fixtures (§20).

  **"Why should I trust a small vendor\'s roadmap?"**                   Because the roadmap is gated and acceptance-tested (§20--§22), the spine has held since the founding vision (§23), and the doctrine ships beside an honest delta, not a status claim.
  --------------------------------------------------------------------- -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

**Appendix G · Deployment topologies & sizing**

  ------------------------- ---------------------------------------------------------------------------- --------------------------------------------------------------------
  **Plant size**            **Indicative footprint**                                                     **Notes**

  **Single line / pilot**   Core + PostgreSQL + small time-series store; 1 worker                        SaaS or a single on-prem host; the current build\'s posture

  **Full plant**            Core + PostgreSQL + partitioned time-series store; 2--3 workers; HA option   Backfill sized to history; restore drill verified (§16.4)

  **Multi-site group**      Per-site collectors → shared core; dedicated or on-prem per data policy      One entitlement resolver and isolation rule set across all (§16.1)
  ------------------------- ---------------------------------------------------------------------------- --------------------------------------------------------------------

**Appendix H · Competitive positioning matrix**

  ----------------------------- ---------------- ------------------------ ------------------ --------------------------------------
  **Dimension**                 **Generic BI**   **Bespoke DS project**   **Black-box AI**   **PlantProcess IQ**

  **Manufacturing semantics**   No               Custom, one-off          Hidden             Built-in (genealogy, defects, value)

  **Reusable across plants**    Partial          No                       Varies             Yes --- config from HMI

  **Evidence & method shown**   No               Sometimes                No                 Always (handles + named method)

  **Touches control?**          No               No                       Varies             Never --- read-only

  **Euro impact**               Manual           Custom                   Rarely             Bounded value engine

  **Data sovereignty**          Varies           Varies                   Often external     Self-host / BYO model / air-gapped
  ----------------------------- ---------------- ------------------------ ------------------ --------------------------------------

**Appendix I · Build backlog mapping --- REDIRECTED (v8.1)**

The wave-to-gate-to-phase mapping was superseded with §20. The work ledger is **Backlog v23** (frozen IDs); the plan is **Roadmap v9**. The acceptance gates of §22 remain in force and are cited by ID from the backlog.

## v8.1 Changelog (18-Jul-2026)

Amended per the Founding-Documents Review directives (15-Jul), evidence-checked against the live tree 18-Jul: (1) derivation line to concept.md v1.1 restored; (2) §7.12 THE SUPERVISOR added - the constitution's keystone, absent from v8; (3) §20 + Appendix I replaced by redirects to Roadmap v9 / Backlog v23; (4) §21 retired as an immutable 26-Jun snapshot; (5) §4.7 connector table regenerated from `Connectors/` code + demo evidence, resolving the 4.7-vs-21 contradiction by construction; (6) §9.1 marked PENDING RULING 1.2.B (tier canon); (7) §10.2 localStorage rows corrected - retirement verified and gate-named (P2-T05); (8) version bumped to v8.1, Part I only. The Interactive Workspace Doctrine v1 is incorporated by reference in §8. v8.0 is preserved unmodified in the archive.
