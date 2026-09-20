# PlantProcess IQ — Industrial Integration Engineering Review

## Enterprise acquisition, interface configuration, DataBlock field definitions, Jobs and Dump Store

**Report revision:** 1.0  
**Review date:** 16 September 2026  
**Document class:** Advisory engineering analysis and proposed change package  
**Decision state:** Recommendations for owner approval; not an implementation instruction or release certificate  
**Requested deliverable:** One Markdown report covering engineering validation, chapter changes and backlog changes

> **Authority boundary.** This report does not amend the six design chapters, create a seventh functional authority, change the current backlog, reopen a closed task, or certify a running product. Accepted recommendations must be integrated into their owning design chapters and then into the single execution backlog. Historical implementations and accepted closures remain evidence of their original scopes. **Design is the destination; repository evidence describes implemented reality; the backlog is the execution and closure path.** [P03] [P07]

> **Example boundary.** `PLCMessages-1786790425225.csv` is an illustrative example of a possible interface catalogue. It is **not** the customer's final source inventory, a production traffic measurement, a selected deployment, or permission to hardcode its plant vocabulary. The design covers OPC/PLC, SQL Server, Oracle, PostgreSQL, MySQL, Excel and CSV together. [U01] [P08]

---

## Contents

- [Executive decision](#executive-decision)
- [Part I — Analysis, validation and proposed engineering](#part-i)
  - [1. Evidence, scope and confidence](#evidence)
  - [2. Requirements and engineering invariants](#requirements)
  - [3. Validation against established industrial solutions](#industrial-practice)
  - [4. Existing design: retain, clarify and extend](#existing-design)
  - [5. End-to-end architecture and ownership](#architecture)
  - [6. Interface configuration and frontend contract](#frontend)
  - [7. DataBlock field-definition and decoding contract](#field-definitions)
  - [8. Acquisition and recording semantics](#recording)
  - [9. Relational and file-source engineering](#providers)
  - [10. Backend services, APIs and lifecycle](#backend)
  - [11. Jobs, continuous execution and downstream readiness](#jobs)
  - [12. Dump Store logical and physical design](#dump-store)
  - [13. Capacity, retention and recovery](#capacity)
  - [14. Security, deployment and operational acceptance](#security)
  - [15. Validation plan and executed reference models](#validation)
- [Part II — Recommendations for the design chapters](#part-ii)
  - [16. Chapter-by-chapter change register](#chapter-register)
  - [17. Proposed chapter clauses and detailed insertion specifications](#chapter-clauses)
- [Part III — Recommendations for the backlog](#part-iii)
  - [18. Existing-task dispositions](#existing-backlog)
  - [19. Candidate new work packages](#new-backlog)
  - [20. Dependencies, release boundaries and adoption sequence](#delivery)
- [Appendix A — Illustrative configuration](#appendix-a)
- [Appendix B — Example-file calculations, not production sizing](#appendix-b)
- [Appendix C — Decisions requiring explicit approval](#appendix-c)
- [Sources and traceability](#references)

---

<a id="executive-decision"></a>
## Executive decision

**The owner's three recording choices are appropriate for a professional industrial product:** record on a chosen time interval; record on a qualified value change; or record when a configured trigger/counter condition occurs. Established industrial products expose comparable capabilities. They also distinguish connection/update mechanics from logging conditions, group membership and storage behavior. The proposal should therefore be adopted as the **simple user-facing model**, not expanded into an unnecessarily complicated operator experience. [E01] [E02] [E03]

However, the three choices alone do not define a dependable enterprise system. The implementation must also make field identity, source type, decoding, event consistency, actual source capability, durability, replay, source impact and finite storage explicit. **A configuration that accepts a number is not proof that the system can execute it.**

### Recommended target

**One configuration experience, one governed execution system, and one logical Dump Store; provider-specific adapters and validated source-specific capabilities underneath.**

```text
Customer sources
  OPC UA / PLC gateway | SQL Server | Oracle | PostgreSQL | MySQL | Excel | CSV
                                      |
                         Source-side adapter / collector
                                      |
                 Selected datasets + declared fields / layouts
                                      |
                 Versioned acquisition and recording policies
                                      |
                    Bounded durable collection and delivery
                                      |
                PPIQ Dump Store: source-shaped accepted records
                                      |
                   Published Mapping / Canvas transformation
                                      |
                     Canonical Plant Data -> BI / Analytics / ML

Configuration and Jobs govern the entire path; the browser is not the runtime.
```

### Principal decisions recommended

| Decision | Recommendation | Why it matters |
|---|---|---|
| Unit of configuration | Connection -> datasets -> stable fields -> capture groups/policies | A customer database, a PLC DataBlock and a single signal are not interchangeable units. |
| OPC topology | OPC-UA-first through an embedded server or proven gateway; PPIQ owns the client/collector | Avoid implementing a new OPC server and every PLC-native protocol without a demonstrated need. |
| User experience | Keep Time / Value change / Trigger-counter as the main choices | Fits the owner's workflow and comparable industrial product patterns. |
| Raw DataBlock input | Provide a first-class layout and field naming editor | Addresses, types, offsets and stable identity are necessary before useful configuration is possible. |
| Recording correctness | Distinguish acquisition method, qualifying condition, record shape and persistence boundary | Prevents false periodic history, missed-trigger claims and accidental whole-group duplication. |
| Job integration | Reuse the existing Job authority; add continuous-executor semantics explicitly | No private scheduler, no job per sample, and no endless session blocking completed batches. |
| Dump Store | Dataset-oriented logical tables over governed storage; one authoritative payload representation | No automatic database-per-source, table-per-signal or uncontrolled duplication of raw payloads. |
| Capacity | Validate source, collector, tenant/site and storage budgets together | A thousand individually valid fields can collectively overload the installation. |
| Retention | Give source datasets their own lifecycle, distinct from log retention | T-199's log-history scope does not establish source-data retention. |
| Release truth | Advertise only the exact implemented, supported, configured and qualified behavior | SDK support and successful configuration tests are not source commissioning. |

### Readiness verdict

The reviewed design contains **substantial reusable foundations**: read-only collectors, dataset registration, stable incremental cursors, atomic staging/receipt/checkpoint behavior, immutable definitions, Job binding, canonical projection, source-time provenance, retention concepts and capacity certification. It should **not** be replaced by a second integration architecture. [P02] [P03] [P06] [P07]

The main required enhancement is an explicit **industrial acquisition contract** connecting these foundations: raw/typed field definitions, user-selectable recording policies, event and buffer semantics, continuous Job execution, coherent Dump Store read models, and a common capacity/retention configuration experience.

**Validation conclusion:** the approach is technically defensible and aligned with documented industrial practice, subject to the constraints in this report. It is **not yet an implementation-complete design approval, interoperability certification, security certification or performance guarantee**. The acceptance matrix below defines what must be demonstrated to earn those claims.

---

<a id="part-i"></a>
# Part I — Analysis, validation and proposed engineering

<a id="evidence"></a>
## 1. Evidence, scope and confidence

### 1.1 Evidence classes used in this report

| Label | Meaning |
|---|---|
| **Owner requirement** | Explicitly requested or ruled in the present conversation. |
| **Reviewed source** | Supported by an identified design/backlog passage or source export. |
| **External verification** | Supported by the cited official specification or vendor documentation. |
| **Engineering inference** | A conclusion drawn from those sources, not a quotation or pre-existing requirement. |
| **Recommendation** | A proposed PPIQ choice; requires acceptance and integration into the owning authority. |
| **Reference-model result** | A local arithmetic/semantic check performed for this review, not a product test. |
| **Unverified** | Current implementation, latest document body or site behavior not established by the available evidence. |

Unless a paragraph explicitly describes reviewed or external behavior, the detailed target engineering below is a **recommendation**. Proposed `MUST` clauses in Part II become binding only after approval and insertion into the official chapters.

### 1.2 Internal source coverage

| Source | Material actually available to this review | Limitation |
|---|---|---|
| Chapter 3, illustrated v4.10.3 | Authority/read contracts; DF1-DF5; relevant B/F surfaces; staging, field, Job, observation and topology sections | Relevant passages read, not a claim of every illustrated page and image being audited. |
| Backlog v2.23.0, Worker Updated, 15 Sep 2026 | Authority tab and relevant task descriptions, acceptance, dependency and worker records | Status is the workbook's recorded status, not a live repository certification. Text extraction is bounded; it is not used as a full-workbook task census. |
| Chapters 1, 2, 5, 6, available v4.10 copies | Relevant product, collector, tutorial, sizing and ownership clauses | These files identify themselves as v4.10. They are not relabelled v4.10.3. Later package statements say other chapter bodies were retained, but current-copy equality was not independently hashed. |
| Chapter 4 | Current references from v4.10.3 Chapter 3/backlog to scheduling, dependency and execution contracts; older mounted body for historical structure | The full current v4.10.3 body was not recovered. Recommendations identify owning sections but are not presented as a verified redline of that whole current file. |
| Repository exports, 14 Sep 2026 | Previously retrieved OPC capability, connector and edge-agent paths relevant to this conversation | No current Git HEAD, complete repository audit, build or runtime test executed in this report. |
| Illustrative PLC CSV | Complete local CSV parsed with a CSV parser that respects quoted multiline fields | Not a real deployment specification or a runtime measurement. |

File access was inconsistent: some raw materializations were unavailable and some later reads returned no content despite earlier successful source reads. Those tool responses are **not evidence of absent source content**. This report uses successful retrieved passages and bounded local extracts, and states unrecovered coverage explicitly. [P01]-[P09]

### 1.3 What was actually validated

- Official OPC UA, PTC Kepware, Inductive Automation, Siemens, Beckhoff, database-vendor and PostgreSQL documentation was consulted for the specific behaviors cited.
- Relevant current design/backlog passages were compared with the owner's requirements.
- The supplied example CSV was parsed in full and its conditional volume arithmetic recomputed.
- Thirty-seven local reference-model checks were executed for arithmetic, example layout, change predicates, identity, cursor and failure-boundary reasoning.

**Not executed:** a real OPC session; a PLC consistency test; source CDC; a PPIQ database migration; a browser journey; product unit/integration suites; a throughput, latency, restart or HA benchmark. Therefore, this review cannot mark an existing task green or establish 1 ms production support.

### 1.4 Current planning basis

The reviewed current authority tab states **M2 approximately one month from the owner's planning checkpoint** and **M3 45 days after M2 completion**. Historical September/October dates remain history. This report sets no new delivery date and supplies no fabricated effort estimate. [P03] [P07]

---

<a id="requirements"></a>
## 2. Requirements and engineering invariants

### 2.1 Owner requirement register

| ID | Requirement | Required design result |
|---|---|---|
| R01 | All named source families participate in the same workflow | Provider-specific acquisition, common downstream contract. |
| R02 | Configure through the DB Link / Interface page | One coherent setup flow using existing B1-B5 surfaces/components. |
| R03 | Time-based recording is selectable | Explicit period, freshness semantics, missed-tick policy and qualified rate. |
| R04 | Value-change recording exposes a band | None/absolute/percent with unit, baseline and quality behavior. |
| R05 | Trigger/counter recording selects a monitored source | Stable field reference, condition, arming/reset behavior and capture members. |
| R06 | Fields inside a DataBlock can be named during configuration | Layout editor with address, type, stable identity, local key and display name. |
| R07 | Raw byte addresses may have no source names | Names are user-authored; decoding is never inferred from a label. |
| R08 | Every provider feeds Dump Store before canonical transformation | One governed ingress path with source-shaped records and provenance. |
| R09 | Explain whether sources/DataBlocks become tables | Dataset is the logical unit; physical layout is explicitly governed. |
| R10 | Link configuration to Jobs | Existing Job authority manages finite and continuous work. |
| R11 | Avoid unlimited growth from PLC plus database/file inputs | Aggregate admission, bounded buffers, retention, archive and measured growth. |
| R12 | Example sheet is only an example | No plant-specific defaults, enums, schemas, rates or estimates inferred from it. |
| R13 | Backend and frontend both need a design | Define APIs, persistence, validation, UX states, permissions and failure behavior. |
| R14 | Validate professional/enterprise engineering | Compare primary sources; distinguish conventions from formal standards. |
| R15 | Recommend changes by chapter and task | Preserve existing owners, closures and authority rules; expose genuinely new scope. |

### 2.2 Invariants that recommendations must not weaken

1. **Read-only toward customer process systems.** No PLC command, process-value write, source acknowledgement that consumes another application's buffer, automatic security downgrade or autonomous source reconfiguration.
2. **Generic same-binary product.** Customer addresses, names, units, layouts, keys, groups and policies are data. A new supported customer configuration requires no customer-specific C#, TypeScript or product-SQL patch.
3. **One authority per concern.** Reuse canonical field/dataset definitions, immutable lifecycle, Source Time Authority, Job scheduling/admission, cursor authority, capability truth and projection services.
4. **Three application schemas.** `ppiq_meta`, `ppiq_staging` and `ppiq_plant`; no vendor/customer schema proliferation. An explicitly approved artifact store is not a hidden fourth functional authority.
5. **No invented history.** Do not fabricate missed counter payloads, retroactive samples, fresh timestamps for stale values or atomic snapshots without source evidence.
6. **No silent information loss.** Explicit selection/deadband is intentional filtering; accidental queue loss, parsing loss and unsupported rates are observable failures/degradation.
7. **No universal rate guarantee.** Requested, negotiated and measured behavior are different facts.
8. **Evidence-safe replay.** A retry must not duplicate the same accepted source revision; a legitimate new event/update must not be discarded merely because its values are equal.
9. **No uncontrolled growth.** Admission, retention and operational reserve are part of activation, not optional administrator cleanup later.
10. **Preserve closed work.** A new requirement does not invalidate a prior closure. New acceptance slices must be named and estimated separately. [U01] [P03] [P07]

---

<a id="industrial-practice"></a>
## 3. Validation against established industrial solutions

### 3.1 Comparison: what the evidence supports

| Official source | Verified behavior or constraint | Consequence for PPIQ |
|---|---|---|
| PTC Kepware DataLogger | Configurable static logging, data-change logging and monitored-item-driven group logging; update and recording settings are separate | The owner's three main choices have direct industrial analogues. Do not copy vendor defaults as universal limits. [E01] |
| Inductive Automation transaction groups | Groups operate in the gateway; read-on-execution differs from consuming subscription values | The browser configures, while an executor runs. Cached and read-after-trigger captures must be visibly distinct. [E02] |
| Ignition Historian | History configuration includes sampling/deadband behavior; implementation differs by historian provider/version | Support explicit recording policies, but do not equate PPIQ's policy with an undocumented vendor compression algorithm. [E03] |
| Ignition Store and Forward | Buffering and disk-backed recovery separate collection from database availability for applicable paths | Durable handoff and backlog monitoring are required; memory-only acknowledgement is insufficient for a durable claim. [E04] |
| OPC Foundation, Part 4 | Sampling, monitored-item filters, reporting queues and triggering are different services/parameters | Model the distinctions explicitly; ordinary subscription does not establish periodic recording or a coherent event snapshot. [E05]-[E10] |
| Siemens STEP 7 documentation | DB addressing has typed width/bit rules; optimized access differs from absolute addressing | A field editor needs a validated layout and transport capability, not just an address/name pair. [E12] [E13] |
| Beckhoff oversampling | High-speed samples may be captured locally and transported as buffered data with timing information | A high-speed source-buffer path is different from running a central timer at 1 ms. [E14] |
| SQL Server documentation | Change Tracking and CDC expose different histories and require source-specific setup | Do not offer one generic “CDC” capability based solely on successful login. [E15] [E16] |
| Oracle/MySQL/PostgreSQL documentation | Change feeds depend on redo/binlog/logical-decoding configuration, positions and retention | Treat each adapter's resume and source-impact contract separately. [E17]-[E20] |
| PostgreSQL documentation | JSONB is not byte preservation; partitioning, uniqueness and RLS have explicit constraints | Choose and test physical storage deliberately; neither a JSON column nor a partitioned table automatically solves evidence or scale. [E21]-[E24] |

### 3.2 What “enterprise standard approach” means here

There is no single industrial standard requiring all historians, gateways and integration platforms to use one database schema, one polling period or one user interface. OPC UA standardizes protocol behavior. Vendor products demonstrate established solution patterns. PPIQ must select a coherent product contract that fits its own rules.

**Recommended pattern:** a versioned control plane; source-side adapters; bounded data-plane execution; explicit recording semantics; durable, idempotent delivery; managed storage lifecycle; and verified observability/security. This is an engineering synthesis of the evidence, not a claim that a specification mandates the entire PPIQ architecture.

### 3.3 Corrections to avoid carrying earlier discussion errors forward

| Earlier shorthand | Correct engineering statement |
|---|---|
| “Sampling interval exists, so periodic recording is covered.” | Monitoring frequency does not require a notification or stored row for every period. Periodic recording needs its own contract. |
| “SetTriggering implements a snapshot.” | It controls reporting of queued monitored-item notifications. Atomicity and event association remain separate obligations. [E07] |
| “Small timestamp skew proves one process snapshot.” | Skew is a temporal check, not source atomicity proof. A constant value may retain an old source-change timestamp. [E09] |
| “No deadband captures every physical change.” | It preserves eligible **observed and representable** changes. Source scan rate, sensor resolution, queues and outages still matter. |
| “Negotiated 1 ms means achieved 1 ms.” | It is a server/adapter setting; end-to-end measured behavior is separately qualified. [E05] |
| “A PLC driver is absent, therefore the product is incomplete.” | OPC-gateway-first can be an intentional complete boundary. Native drivers are additional capabilities, not automatic obligations. |
| “One shared JSONB table is always the enterprise design.” | It is one useful bounded baseline. Typed tables/segments may be justified by measured workloads; the logical dataset contract must remain stable. |
| “Retention is missing everywhere.” | Relevant retention and capacity foundations already exist. The gap is acquisition-specific, dataset-wide lifecycle integration and implementation proof. [P03] [P06] |
| “T-266 implements the OPC feature.” | T-266 is design delivery. Executable frontend/backend work needs its own named implementation acceptance. [P07] |
| “The whole current design was reviewed.” | This report has the source-coverage limits stated in Section 1, particularly the unrecovered full current Chapter 4 body. |

---

<a id="existing-design"></a>
## 4. Existing design: retain, clarify and extend

### 4.1 Source-backed findings

| Finding | Reviewed basis | Disposition |
|---|---|---|
| F01: collector/read-only boundary exists | Ch2 §2.0.9; Ch6 §6.1.1.4 | Retain; specify source-specific responsibilities and the configuration transport. [P02] [P06] |
| F02: connections, datasets and fields are separate authorities | Ch3 DF1/DF2 and §4.5.5 | Extend the existing definitions; do not add a competing tag registry. [P03] |
| F03: incremental cursor correctness is already designed | Ch3 DF3 v4.10.3; T-108 | Reuse ordered tuples, source revisions and atomic commit; extend source adapters without weakening the contract. [P03] [P07] |
| F04: recording policy is not sufficiently connected to field/group/job/storage contracts in reviewed clauses | T-225 interval/deadband scope; T-266 missing-workflow entries; Ch3 reviewed interface sections | Add an explicit contract. This is a scoped coverage finding, not proof based solely on keyword absence. |
| F05: DataBlock field layout/naming needs first-class treatment | Current field catalogue describes dataset/column names; example interface catalogue describes whole messages | Add stable field identity, address/layout version and validated decoders. [P03] [P08] |
| F06: physical versus logical staging tables are ambiguous | DF2 names `stagingTableName`/first-import creation; DF3 and §4.5.3 name common `staging_records` | Resolve explicitly rather than assuming both are independent payload stores. [P03] |
| F07: Job import target is a deliberate exception | Ch3 §4.5.5a | Preserve dataset binding; do not force import jobs to impersonate transformation jobs. [P03] |
| F08: endless acquisition and finite downstream batches need separate lifecycles | Current DF3 terminal-success rule; current Job authority | Add continuous-session/micro-batch semantics without releasing partial batches. |
| F09: source-time and signal semantics exist | Ch3 §4.5.4; T-216/T-233 and aggregation contracts | Reuse them. A Counter signal kind is not a counter-trigger engine. [P03] [P07] |
| F10: log retention is not source dataset retention | T-199 F9 scope | Retain log ownership; allocate source-data lifecycle separately. [P07] |
| F11: capacity certification exists but is not a universal acquisition guarantee | Ch6 §§6.1.5.8, 6.1.9 | Extend workload dimensions to values/events/bytes/source load; do not claim published reference bands are measured. [P06] |
| F12: dangling retention reference | Ch3 observation table text points to “Chapter 7” within a six-chapter authority model | Correct the reference to the actual owning policy sections; do not invent Chapter 7. [P03] |
| F13: source exports show foundation/scaffolding, not this complete runtime | Named 14 Sep capability/edge files | Keep implementation status separate from target recommendations. [P09] |

### 4.2 Implementation observations, narrowly scoped

The retrieved 14 September source export includes an OPC capability registry that declares real browse/read/subscription/session operations unavailable, a legacy connector performing configuration validation, and an edge sample script producing deterministic samples rather than a commissioned continuous collector. These observations concern those specific paths and that snapshot. They do not establish the state of every later branch or every provider. [P09]

The legacy provider metadata and the API truth registry also require convergence. The reviewed backlog already distinguishes T-254's frozen earlier UI/website work from an outstanding provider-authority extension. Use that named correction; do not reopen T-207 merely to implement future live capabilities. [P07]

---

<a id="architecture"></a>
## 5. End-to-end architecture and ownership

### 5.1 Three planes

**Control plane:** configuration, field/layout versions, source capabilities, approvals, Jobs, admission, retention policy and audit. It defines what is permitted and which exact version runs.

**Data plane:** source reads/subscriptions, decoding, qualifying records, capture assembly, durable spool, delivery and Dump Store acceptance. It executes bounded authorized work.

**Semantic plane:** published Mapping/Canvas definitions turn staged data into canonical plant facts. Analytics, BI and ML consume canonical data and governed outputs, not arbitrary Dump Store content.

This separation prevents a decoder from becoming a second business-rule engine and prevents a page-local timer from becoming a scheduler.

### 5.2 Logical model and cardinalities

```text
Tenant / Site
  -> Collector binding(s)
  -> Connection profile(s)
       -> Dataset(s)
            -> Stable field identities + immutable schema/layout versions
            -> Acquisition configuration revision(s)
            -> Recording group(s) / output stream(s)
            -> Import Job binding
                 -> finite run OR continuous acquisition session
                      -> bounded completed import batches
                           -> source-shaped accepted records
                           -> receipt/checkpoint/evidence
                                -> published transformation execution
                                     -> canonical facts and lineage
```

A connection is an access boundary, not a table. A dataset identifies a coherent source selection and record contract, not necessarily one physical relation. The same PLC DataBlock can contribute to several datasets; the same selected source field can serve several recording policies. Reuse source acquisition only when tenant, security, source identity, required timing and filter semantics are compatible.

### 5.3 Source-side placement

An OPC server may be embedded in the PLC or provided by a gateway with a qualified native driver. PPIQ's OPC client resides in the approved source-side collector. Relational/file collectors similarly access approved sources from their allowed network zone.

A collector-initiated authenticated channel can fetch approved work/configuration and push data/status. **Outbound initiated does not mean physically unidirectional:** acknowledgements and responses still traverse the connection. A true data-diode deployment requires a different, explicitly qualified transfer protocol. Do not claim one by drawing a one-way arrow.

Source secrets remain in an approved secret store available to the executing collector. The core persists references and policy metadata, not PLC passwords needed for direct core-to-OT connections. A customer-uploaded Excel/CSV file is a different ingress case: it can be processed in an isolated ingestion service because no connection into OT is required.

### 5.4 Capability truth is multidimensional

A single `supportsOPC=true` flag cannot carry the product promise. Record distinct facts:

| Axis | Example |
|---|---|
| Adapter implementation | Browse method exists and has executed tests. |
| Supported source profile | Version/security/type combination is supported by this adapter build. |
| Configured authority | Credentials, trust, selectors and field contracts are complete. |
| Connection/source probe | This endpoint accepted the tested operation. |
| Semantic capability | Periodic fresh-read, cached capture or source-event snapshot is supported. |
| Negotiated settings | Server-revised sampling, publishing and queue values. |
| Qualified workload | Tag count, payload, rates, outages and latency envelope tested. |
| Current operational state | Connected, retrying, buffering, blocked, stale, gap detected. |

Advertise the exact combination. A successful connection test does not establish CDC, deterministic event capture, field-layout correctness or storage capacity.

---

<a id="frontend"></a>
## 6. Interface configuration and frontend contract

### 6.1 Unified journey without duplicate pages

Use existing B1-B5 contracts as a connected configuration experience. B1 owns connection access; B2 owns registered data; B3 owns preparation/acquisition settings; B4 owns ingestion results; B5 owns Jobs. The interface may use a wizard, tabs or linked drawers, but must not establish a second source registry or Job scheduler.

Recommended steps:

1. **Connection:** source family, location, collector, authentication/trust and read-only capability.
2. **Select Data:** source tables/views, file sheets/ranges, OPC nodes or DataBlocks.
3. **Fields & Layout:** include/exclude, native types, stable field names, raw decoding and identities.
4. **Acquisition & Recording:** provider-appropriate reading plus the owner's recording choices.
5. **Storage & Retention:** accepted record shape, retention, archive, capacity and quota preview.
6. **Job:** finite/continuous binding, schedule/window, retry, concurrency and downstream handoff.
7. **Validate & Activate:** semantic validation, source probe, workload admission and exact-version activation.

Saving a draft, testing a source, publishing a valid configuration and activating a running acquisition are different operations. Discovery must not start bulk ingestion.

### 6.2 Provider-dependent connection controls

| Provider | Required configuration categories |
|---|---|
| SQL Server | Server/instance or explicit endpoint; database; auth method/secret reference; encryption/trust policy; collector; supported SQL/CDC capability. |
| Oracle | Host/port; explicitly selected service-name or SID mode; authentication; approved wallet/TLS references when required; collector and source permissions. |
| PostgreSQL | Host/port/database; authentication; TLS verification; collector; optional separately commissioned logical-decoding capability. |
| MySQL | Host/port/database; authentication/TLS; collector; optional separately commissioned binlog capability and position contract. |
| Excel | Upload or approved file location; workbook and sheet/table/range selection; header/type/date/formula policy; readiness rules. |
| CSV | Upload/drop source; pattern; encoding, delimiter, quote/escape/header/null rules; readiness and replacement policy. |
| OPC UA | Endpoint; application identity/trust; user identity; security policy/mode; collector; browse/read/subscription capability; source type/layout contract. |
| Optional native PLC adapter | Only when explicitly implemented and approved: controller/protocol profile, read address access and decoder; never implied by OPC support. |

Use secure references, not plaintext secret re-display. Server-side validation owns legality; the UI schema merely renders it. Provider-specific terminology must remain accurate instead of forcing every source into `host/schema/table` fields.

### 6.3 Data selection and bulk configuration

Support multiple datasets under one connection; bulk field import/paste; searchable/virtualized field grids; per-group policies; visible overrides; reusable layout templates with explicit version references; and preview limited by source budgets.

A thousand fields should not require a thousand manual dialogs. Conversely, “select all” must not silently apply the fastest available monitoring rate or record every discovered address.

Bulk deadbands require compatible numeric types and declared units/ranges. Applying `0.1` simultaneously to temperature, pressure and a counter without unit context is invalid. A parent group may supply defaults, but the UI must show the effective per-field configuration and override origin.

### 6.4 Recording controls visible to the ordinary user

| Mode | Required primary controls | Additional controls when relevant |
|---|---|---|
| Time | Period; selected fields; fresh-read or validated-cache capture; unchanged values policy fixed/explained for periodic mode | Missed tick behavior, maximum age/availability proof, phase/window. |
| Value change | Selected fields; monitoring rate; None/Absolute/Percent band; comparison unit/basis | Quality change behavior, optional maximum recording interval, initial baseline handling. |
| Trigger / counter | Trigger field; condition; monitoring rate; selected capture members; capture strategy | Debounce/re-arm, startup/reset/wrap policy, permitted uncertainty and event identity. |

Display **“monitor every”** separately from **“record when.”** Display the requested, negotiated and measured states separately. A trigger-only monitoring field does not automatically receive its own full historical stream.

### 6.5 Capacity preview and activation

The preview presents selected field/record counts, source read impact, expected and worst-case accepted rates, serialized bytes, hot storage growth, raw/canonical retention, local outage coverage, existing site load and required reserve. Estimates show their basis and confidence. Unknown event rates or payload types are labelled unknown, not zero.

A low-load preview is not an endurance benchmark. Activation requires an approved capacity profile; advanced rates outside that envelope remain draft/refused until qualified. The UI may offer an explicitly approved slower alternative, but never silently substitute it.

### 6.6 Ten-field page specification to integrate into B1-B5

| Existing page-contract field | Required extension |
|---|---|
| AIM | Configure a source through to a governed Job and inspect its accepted data without customer code. |
| ROLES | Separate connection/secret administration, dataset authorship, field-layout editing, publication/activation, raw preview and Job operation. Reuse the existing role authority. |
| LAYOUT | Source summary, dataset tree, main configuration panel, field grid, validation/capacity summary and actions. Reuse established components/tokens. |
| CONTROLS | Provider-aware inputs; stable field selector; group policy editor; layout grid; bounded preview; validate; publish; activate; pause; navigate to Job/batches. |
| HOOKS | Existing API-resource, save/concurrency, safe-action and toast/error mechanisms; no browser acquisition loop. |
| CALLS | Existing connection/dataset/job families with the proposed extensions in Section 10. |
| STATES | Empty, loading, configured, dirty draft, validating, invalid, unsupported, awaiting approval, active, buffering, gap/degraded, paused, failed and capacity-blocked. Distinguish configuration from live execution. |
| SELECTIONS | Setup selections scope configuration; they are not analytical cross-filter state. |
| EMPTY-INSTALL | No customer source, address, field name, rate or layout prefilled as a real plant. Product grammar/templates may exist without customer facts. |
| A11Y + RTL | Full keyboard grid editing, labelled type/address controls, textual errors, focus management, virtualized-row accessibility, logical layout directions and Arabic RTL verification. |

Errors must name the offending field and corrective action, without exposing credentials, cross-tenant names or stack traces. A role denied raw preview must not see sample values inside a validation error.

---

<a id="field-definitions"></a>
## 7. DataBlock field-definition and decoding contract

### 7.1 Required identity separation

| Element | Purpose | Change rule |
|---|---|---|
| Stable `field_id` | Durable reference used by capture groups, triggers, mapping and lineage | Never regenerated merely because a label changes. |
| Source locator | DB address, node identity or structured member path | Changes require a new source binding/layout revision. |
| Technical field key | Source-shaped column name exposed by the dataset | Unique within the dataset revision; renames have explicit compatibility/impact treatment. |
| Display name | Human-readable/localized name | Presentation change; does not retarget source reads. |
| Description | Engineering explanation of the field | Audited metadata; not a decoder. |

Names are assigned inside PPIQ, not written back to the PLC. A field called `Temperature` remains unverified until its source address/type/layout is established. For UA nodes, retain namespace URI and identifier with source identity; an index alone may change across sessions. [E10]

### 7.2 Example layout

This example is deliberately unrelated to any real customer's commissioned layout.

| Source locator | Declared type | Width | Technical key | Role |
|---|---|---|---|---|
| `DB20.DBB1` | BYTE | 1 byte | OperatingState | Payload |
| `DB20.DBW2` | INT | 2 bytes | OperatingMode | Payload |
| `DB20.DBD4` | REAL | 4 bytes | Temperature | Payload |
| `DB20.DBD8` | REAL | 4 bytes | Speed | Payload |
| `DB20.DBD12` | REAL | 4 bytes | Pressure | Payload |
| `DB20.DBD16` | UDINT | 4 bytes | SaveCounter | Trigger + event identity |
| `DB20.DBX20.0` | BOOL | 1 bit | SaveTrigger | Optional trigger |

Siemens' address prefixes indicate address/width categories, not the complete interpretation of the payload. In particular, a double-word address does not decide whether the value is REAL or an integer. Optimized data access also restricts absolute-address assumptions. [E12] [E13]

### 7.3 Layout schema

For raw-byte acquisition, specify:

- Source block/address identity, requested read region, and whether offsets are absolute to the block or relative to the message payload.
- Byte offset, bit offset for bit fields, fixed width or explicit variable-length rule.
- Native type, signedness, byte/word order, string character encoding and header conventions.
- Array element type, count bounds, stride and index base; nested structure member paths.
- Source unit and source quality/time/key roles where documented.
- Layout version/signature and provenance of the layout: vendor export, customer engineering approval or validated source metadata.
- Inclusion, recording and retention assignments using stable field references.

Calculate fixed widths from the type. Validate integer overflow in offset/size calculations and enforce buffer bounds before decoding. No arbitrary customer executable code may be uploaded as a decoder. Support a versioned, bounded declarative grammar; unsupported codecs require an explicitly reviewed adapter extension.

### 7.4 Transport distinctions

**Native/raw bytes:** the adapter can read an approved block region and the layout decodes those bytes.

**OPC ByteString or supported structured payload:** the node must actually exist and be readable. A decoder interprets the returned payload using the declared version.

**OPC typed nodes/members:** use source type metadata and stable node bindings. Do not reinterpret an already decoded REAL through a second byte-order conversion.

A manually entered DB address cannot make an OPC server expose data it does not serve. An import of an interface catalogue proposes bindings; it does not prove those bindings exist or authorize unsafe PLC configuration changes.

### 7.5 Validation and editing behavior

Reject out-of-bounds fields, invalid bit offsets, contradictory lengths, duplicate keys, unsupported types and undeclared overlaps. Allow explicitly declared bitfield members or union/overlay structures only when the decoder contract represents them unambiguously.

For a read beginning at absolute byte 100, a field at byte 108 is decoded from payload offset 8. Do not confuse these coordinate systems.

Bulk rename uses field IDs and schema versioning. Publishing a changed address/type invalidates the old commissioning evidence for that binding; it does not rewrite historical payloads. A new version must be activated at a defined acquisition boundary so a batch never mixes layouts without explicit per-record versioning.

A checksum/signature mismatch can detect some source layout changes. A silent semantic change with identical bytes, size and type may be undetectable without a source layout identifier or human commissioning control. The product must disclose that limitation rather than claim automatic layout-drift detection for every PLC change.

### 7.6 Decoding versus semantic mapping

Decoding establishes the source value. It does not automatically decide canonical parameter identity, equipment, material/analysis subject or business unit conversion.

A source-declared scale/encoding may be decoded when required to interpret the wire representation. A business conversion, such as a source engineering unit to the canonical chosen unit, belongs to an approved transformation with explicit lineage. Do not scale in both gateway and PPIQ because each guessed the other had not done it.

---

<a id="recording"></a>
## 8. Acquisition and recording semantics

### 8.1 The six distinct decisions

| Decision | Question |
|---|---|
| Source update | When does the sensor/PLC/gateway produce or refresh information? |
| Read/sample | When does the adapter request or observe it? |
| Qualify | Does this observation satisfy a recording condition? |
| Publish/transport | When are buffered notifications or records transmitted? |
| Assemble | Is the output one field, a coherent record or a triggered group? |
| Persist | When is the record durably journaled and durably accepted by the core? |

A smaller recording period cannot create source information that does not exist. A slower database flush does not require discarding intermediate accepted records. OPC's sampling/filter/queue model supports this separation but does not define PPIQ's historical record policy. [E05] [E06] [E08]

### 8.2 Proposed user-facing grammar

Keep the main mode small:

```text
recording_mode = PERIODIC | ON_CHANGE | TRIGGERED
source_shape  = SCALAR_FIELDS | STRUCTURED_RECORD | BUFFERED_EVENTS
```

Modifiers include deadband, optional maximum recording interval, quality handling, capture method, freshness/validity requirements and explicit loss tolerance. These are structural product choices; actual addresses, names, thresholds and periods are customer configuration.

`BUFFERED_EVENTS` is not a fourth competing timer. A buffer may be polled periodically while only newly identified contained events are persisted.

### 8.3 Periodic recording

**Proposed meaning:** each due logical capture produces a record or an explicit unavailable/missed-capture outcome, even when the value is unchanged. The mode must declare:

- Requested period and approved effective period.
- Fresh read versus a validated cached value.
- How freshness/continued source observation is established.
- Timing tolerance and missed-occurrence behavior.
- Whether a missing capture becomes a gap record or an absent observation with a bounded gap event.

Use a monotonic clock for local scheduling and UTC/source authority for record time. Schedule from a defined time base rather than accumulating uncontrolled `sleep-after-work` drift. Do not backfill overdue periodic ticks by duplicating the newest value and assigning past timestamps.

A fresh OPC read request with `maxAge=0` is an attempt to obtain a new source value, not an unconditional sensor-sampling guarantee. Check the returned data and timing semantics. [E11]

A cached periodic record is a record of known state under its declared validity contract. Preserve the original source timestamp and separately store capture time/age evidence. A source timestamp may remain unchanged while successful scans continue because the value/status has not changed. Age of that timestamp alone is not a universal stale-data detector. [E09]

### 8.4 Change-qualified recording

**Proposed application semantics:** compare the candidate value in a declared comparison unit/type with the last value accepted into the durable recording journal for that output stream. An initial value is recorded as an initial baseline, not falsely described as a physical change. The default strict absolute predicate is:

```text
record_value_change = abs(candidate - last_recorded_baseline) > threshold
```

`None` means no configured numeric tolerance; type-aware equality still applies. Percent deadband requires an approved fixed engineering range and a declared formula, not a percentage of an unstable current value or an automatically learned range. Invalid ranges refuse activation.

The baseline must not advance on a failed local journal write. Persist/recover baseline state with its sequence/configuration revision, or rebuild it from the journal. Quality transitions are recorded as source evidence according to policy; bad values are never silently converted into good numeric zeros. Explicit rules handle NaN, infinities, signed zero, null/missing and arrays.

**OPC filter warning:** OPC AbsoluteDeadband uses its own last-queued baseline. That is not the same thing as the last durable PPIQ record. A downstream deadband must not silently claim equivalence with an upstream filter. [E06]

Therefore offer two clearly described execution paths: preserve the qualified source notification stream with its source filter semantics, or evaluate the PPIQ recording predicate over a sufficient unfiltered input stream. Filter pushdown is an optimization only when semantic equivalence is proven for that plan. Otherwise refuse the optimization or require an explicit change in the accepted contract. Do not stack two hidden filters.

A change in one field records only that field by default. A deliberate “capture group when any selected field changes” option is different and its larger volume appears in capacity preview. Arrays/whole-record notifications need an explicit element-versus-record policy, never a guessed scalar band over a binary blob.

### 8.5 Very small numeric changes

“No deadband” does not promise detection of every physical variation. The source datatype, sensor resolution, update frequency and transport may already have removed the distinction.

The locally checked Float32 example at value 850 has adjacent representable spacing **0.00006103515625**. Encoding 850.00001 as Float32 produces 850.0. Converting that already rounded value to Float64 cannot restore the difference. This is a calculation on an example datatype, not a statement that the customer's temperature tag is Float32.

Retain source types and lossless encoded values where required. The reviewed canonical `numeric(18,6)` field must not be represented as universally lossless for every source value. Precision-reducing canonical mappings need explicit validation and preserved source evidence. [P03]

### 8.6 Trigger and counter capture

A trigger definition references stable field IDs, never an unvalidated free-text address unconnected to the field catalogue. Define the condition and whether it is **edge-based** or **level-based**.

| Condition | Required behavior |
|---|---|
| Rising edge | One capture on observed false-to-true; sustained true does not repeat. |
| Falling edge | One capture on observed true-to-false. |
| Value changed | Type-aware observed change; may include non-monotonic values. |
| Counter increment | New sequence/progress under a declared counter width/reset/epoch contract. |
| Condition transition | Capture when a bounded predicate changes from false to true, unless an explicit repeated-level policy is selected. |
| Every N events/counts | Define interval crossing and missing increments; testing only `counter % N == 0` is insufficient when observations skip values. |

Specify startup baselining, re-arm, debounce, source quality transitions, reconnect and overlapping-trigger behavior. Default startup does not invent a rising edge merely because the first observed value is true. Debounce is intentional event suppression and must be shown in the policy. Trigger evaluation uses a bounded typed grammar, not arbitrary scripts or unbounded loops.

### 8.7 Capture strategies are not interchangeable

| Strategy | Honest meaning | Appropriate qualification |
|---|---|---|
| Last valid cache | Latest known members at the collector when the trigger is processed | Validity/freshness evidence and explicit asynchronous association. |
| Read after trigger | Values obtained by reads issued after trigger observation | Read latency, source consistency and partial-result policy. |
| Source-latched snapshot | Source freezes members for an identified event | Source publication/consistency protocol; identity and retention duration. |
| Source event record | Source supplies an event payload and sequence/time | Versioned event schema, replay and source position contract. |

OPC SetTriggering can report linked queued samples; it is not a general atomic snapshot primitive. [E07] A multi-node read or one database transaction at the destination also does not prove that the source values belong to one process cycle.

Prefer a source-latched/event-record contract when event association must be exact. A source-published sequence before/after, double buffer or seqlock-style pattern may be used **only where the source protocol documents and guarantees its update ordering**. Reading the same cached sequence twice is not by itself proof that payload reads bypassed stale caches or observed a coherent update.

Record capture strategy, event identity, member validity/quality and source timestamps. Evaluate time skew only under the declared time basis. Do not reject an unchanged valid member solely because its last-change timestamp is old; do not accept mixed cycles just because timestamps are close.

### 8.8 Counter gaps and buffered messages

For an observed counter jump from 1257 to 1260, at most the available source evidence can establish the intervening events. A latest-value read cannot reconstruct the values at 1258 and 1259. Recover them from a documented source buffer/history when available, otherwise record a gap.

Counter decreases require reset/rollover/epoch rules. A modulo difference is valid only under assumptions excluding ambiguous resets or multiple unseen wraps. Prefer a source boot/epoch identity and sufficiently large sequence. Do not compare arbitrary counters across PLC restarts as if they formed one monotonic stream.

For buffered messages, read approved regions, verify publication/validity markers, extract only unseen valid records, persist each event's identity and advance the consumer checkpoint only after durable acceptance. Handle slot reuse, sequence wrap, buffer overflow and partial writes. A record with identical values but a new event sequence is a new event. Re-reading the same slot/version is not.

PPIQ must not acknowledge/reset a process buffer used by an existing controller application. A read-only mirror or dedicated non-destructive feed may be required. Source protocol acknowledgements that do not mutate process data remain separate from a PLC memory write. Undefined `AckRequired`, direction and type enums in an example catalogue are unresolved inputs, not implementation instructions. [P08]

### 8.9 Queueing and rate truth

Retain requested and revised sampling/publishing/queue settings and report failures to attain the approved plan. Queue size one is a latest-value policy, not complete change history. The absence of an overflow flag must not be used to prove no intermediate loss under such a policy. [E08]

Size queues for qualifying event rate, bursts, publication delays, network stalls and drain capacity. A simplistic `publishing interval / sampling interval` calculation is not an outage guarantee. Treat per-item queues, subscription retransmission, local journals and core ingestion queues as separate buffers with separate retention.

Maintain stream-specific metrics for intentional filtering, detected missing sequences, dropped/overwritten notifications and rejected records. Aggregate normal metrics; expose bounded per-field diagnostics on demand to avoid uncontrolled monitoring cardinality.

---
<a id="providers"></a>
## 9. Relational and file-source engineering

### 9.1 Relational datasets

For SQL Server, Oracle, PostgreSQL and MySQL, a connection may expose many selected tables/views. Register each coherent source object/selection as a dataset with its own included fields, identity, acquisition mode, history range and source budget. Do not mirror an entire customer database by default.

Offer these modes only when executable for that provider:

| Mode | Contract |
|---|---|
| Initial bounded load + incremental | Establish a consistent starting boundary; then read source revisions beyond a committed position. |
| Source change stream / CDC | Consume a source-native sequence with explicit insert/update/delete semantics and resume bounds. |
| Scheduled snapshot | Read an approved bounded object/window; declare whether to preserve snapshot versions, compute changes or update a derived current-state view. |
| Manual/backfill | Read a specified historical range under separate throttle and resumable checkpoints. |

Specify source revision, row identity and operation kind separately. The business key alone is not an update version. A payload hash alone cannot distinguish “A -> B -> A” revisions or two intentionally distinct identical events.

### 9.2 Preserve the current total-order cursor contract

Reuse the current v4.10.3 ordered position `(watermark, stable tie-break members)` with provider-native ordering, precision and collation. Cursor, accepted rows and their receipt commit atomically. A retry can re-read, but cannot re-stage the same accepted source revision as a new effect. [P03]

A cursor contract must address concurrent source modifications. Capturing an upper watermark and adding a key tie-break does **not** automatically make a changing source transactionally consistent. Select an explicit source strategy: supported snapshot isolation/bounded consistent read, CDC position boundary, or an approved overlap-and-reconciliation policy. Test inserts and updates occurring during page reads, including rows moving behind the last position.

Where no reliable incremental identity exists, preserve the dataset and offer an approved snapshot/backfill mode with the existing full-scan cadence/load restrictions. Do not silently degrade incremental acquisition into repeated full-table scans.

### 9.3 Provider-specific change semantics

| Provider | Important distinction | Proposed commissioning obligation |
|---|---|---|
| SQL Server | Change Tracking identifies changed rows and supports obtaining their latest state; it is not a history of all intermediate values. CDC is different. | Declare which mechanism is used; verify source setup, retention validity and delete/update behavior. [E15] [E16] |
| Oracle | A redo/LogMiner-based strategy depends on appropriate source configuration, permissions and dictionary/revision interpretation. | Use a qualified adapter/profile, record source position and restart strategy; do not assume an ordinary SELECT account grants change-log access. [E17] |
| PostgreSQL | Logical decoding has source configuration and slot lifecycle; restart can lead to re-delivery, and retained WAL can consume source disk. | Idempotent receipts, monitored slot lag/retention, source-approved limits and a gap/reinitialization procedure. [E19] [E20] |
| MySQL | Binary-log behavior depends on server configuration and available retained log position. | Verify format/identity/position/retention with the supported adapter; handle a missing resume position as a gap, not success. [E18] |

The report does not select a particular commercial CDC product or promise universal database-version support. A supported matrix must identify engine/version, authentication, types, source consistency, operations, reset/restore and resume behavior. Provider feature availability is tested separately from basic connectivity.

Enabling replication slots, CDC capture, supplemental logging or server settings belongs to the customer's authorized DBA/provisioning process. PPIQ may validate and explain requirements; it must not silently alter production configuration. “Read-only” is not the same as “zero source impact.”

### 9.4 Initial history, updates and deletes

The user chooses the initial period, selected columns, row filters and maximum backfill footprint. The configuration preview accounts for that one-time load separately from steady-state growth.

Preserve changes as source-shaped revisions/operations in Dump Store when the chosen mode promises change history. A current-state projection may be updated in place, but must not erase required source evidence. Model deletes as source operations/tombstones when available. Absence from a partial scan, failed file or incomplete snapshot is not a delete.

Downstream canonical handling reuses the existing projection contract. This report does not silently replace existing canonical upsert/supersession semantics with event sourcing everywhere. Any change to canonical historical revision behavior requires a separate explicit owning-clause decision.

### 9.5 Excel and CSV

A workbook is a file resource, not necessarily one dataset. Sheets, named tables or declared ranges may be separate datasets. A recurring CSV feed may be one dataset receiving many file versions.

Configuration must state:

- Source location/upload authority; file pattern; completion/readiness contract, such as producer rename/manifest or a qualified stable-file rule.
- File identity and source occurrence identity; content hash is useful integrity evidence, not necessarily the business-event identity.
- Encoding, delimiter, quotes/escapes, header and duplicate-column rules, null/empty semantics, locale/date system and selected fields.
- Parser version and type declarations; Excel formula/cached-value policy and external-link/macro refusal.
- Whole-file replacement versus append feed; truncation, rewrite and duplicate-delivery behavior.
- Maximum compressed/uncompressed size, sheets/rows/columns, cell length and parsing time; quarantine and operator-visible errors.

Do not execute macros, external links or spreadsheet formulas as an incidental ingestion step. Preserve original file bytes when required by policy; parsed cells/rows are a separate representation. CSV output from the product must address spreadsheet formula injection without altering the authoritative raw input merely to make an export safe. [E26]

A robust receipt can include dataset, producer occurrence/file-version identity, parser revision, selector (sheet/range) and logical record locator. A global content hash dedupe would incorrectly collapse legitimate identical files from different tenants or explicitly different source occurrences. Conversely, a renamed delivery of the same identified file version should not be re-ingested as new.

A growing CSV cannot be checkpointed by arbitrary byte offset alone if multibyte encoding, quoted multiline records, truncation or file replacement are possible. Resume only at verified logical record boundaries with file-version evidence.

### 9.6 Cross-source overlap

The same fact may arrive from direct OPC and the customer's existing database. Preserve source-specific evidence in Dump Store. Deduplicate **transport retries** there, but resolve **business equivalence or source authority** through approved identities and mapping/reconciliation rules.

Possible approved configurations include OPC for low-latency signals and database history for orders/lab results; database-only history where it is adequate; or dual-source reconciliation. Do not ingest both and automatically count every apparent duplicate as an independent physical event. Equally, do not delete one because values and timestamps merely resemble each other.

---

<a id="backend"></a>
## 10. Backend services, APIs and lifecycle

### 10.1 Service responsibilities

The following are responsibility names, not claims that these exact classes already exist.

| Service boundary | Owns | Must not own |
|---|---|---|
| Connection configuration | Provider/collector/access settings, references, validation and audit | PLC-specific business logic or private job scheduling. |
| Capability registry | Implemented operation and source-profile truth | Synthetic success or UI-local capability lists. |
| Dataset/field catalogue | Stable identities, source selection, layout/schema descriptions | Duplicate independent canonical-parameter authority. |
| Acquisition plan compiler | Validated binding of versions, policies, capabilities and budgets | Arbitrary code generation bypassing approved source access. |
| Adapter/decoder | Protocol operations, source-native types and bounded decoding | Customer-specific canonical mapping rules. |
| Acquisition executor | Authorized sample/trigger evaluation and record assembly | Separate schedule grammar or ad hoc core database writes. |
| Durable ingestion writer | Receipts, data, checkpoint, batch completion and delivery acknowledgement | Competing source cursors or unbounded memory buffering. |
| Dataset reader/compiler | Typed, scoped source-shaped access for preview/Canvas/safe SQL | Direct analytical access bypassing canonical projection. |
| Dataset lifecycle service | Payload retention, archive, dependency floors and reclamation | Audit-log deletion policy; T-199 remains log-scoped. |
| Jobs/admission/health | Existing execution, limits, dependency and operational truth | Feature-local substitutes for the canonical authorities. |

### 10.2 Configuration lifecycle

Recommended states are **Draft -> Validated -> Published -> Assigned/Activated**, with explicit pause, supersession and retirement. Reuse the existing immutable lifecycle rather than creating independent state machines with inconsistent meanings.

An acquisition configuration revision binds dataset schema/layout revision, field IDs, recording group settings, source profile reference, source-time policy, output shape, budget and retention policy references. Runtime negotiated settings and measured performance are separate evidence linked to the revision; reconnect must not mutate published content.

The existing definition-kind catalogue is not assumed to accept new acquisition kinds automatically. The owning lifecycle task/extension must explicitly register any required kind, validation schema, dependency types, import/export behavior and compatibility rules. Do not disguise an acquisition plan as an S1 transformation simply to pass a closed enum.

Use optimistic concurrency/version tokens for editing. Publish validates all references server-side. Activation binds an exact revision and deployment capability; a previously valid plan may be refused if the source, trust, capacity or required field layout changed. A secret rotation can be recorded as runtime binding evidence without hashing secret bytes into portable definitions.

### 10.3 Proposed API extensions

Retain the existing connection, dataset, definition, import and Job endpoint families. The paths below are target contracts for review, not a verified inventory of running endpoints. [P03]

| API family / proposed operation | Behavior |
|---|---|
| `GET /api/connections/catalog` | Provider schemas and truthful executable capabilities. |
| `POST/PUT /api/connections...` | Create/update references and source configuration with concurrency control. |
| `POST /api/connections/{id}/test` | Dispatch an authorized bounded probe to the assigned collector; return operation-specific evidence. |
| `GET /api/connections/{id}/discover` | Budgeted, paginated source-object discovery; continuation and partial state explicit. |
| `POST /api/datasets` | Register a dataset and source selector; no automatic unbounded import. |
| `PUT /api/datasets/{id}/fields` | Edit field selection, stable identities and a draft schema/layout. |
| `PUT /api/datasets/{id}/acquisition` | Edit recording/capture settings through the common lifecycle. |
| `PUT /api/datasets/{id}/retention` | Edit proposed dataset policy subject to dependency floors and impact preview. |
| `POST /api/datasets/{id}/validate` | Structural, semantic, capability and source-binding validation. |
| `POST /api/datasets/{id}/capacity-preview` | Expected/peak demand, evidence basis, current reservations and refusal reasons. |
| `GET /api/datasets/{id}/preview` | Typed scoped read with sample origin and configuration revision; no analytics claim. |
| Existing `/api/jobs/...` operations | Bind/run/pause/cancel through existing authorities; no provider-private scheduler. |
| Collector ingestion family | Authenticated batch submission/status/acknowledgement with idempotency and bounded envelopes. |

Every operation requires tenant/role scoping. Long probes/discovery return an operation identity rather than holding a browser request indefinitely. A preview or test never grants publication rights or bypasses rate limits.

### 10.4 Validation order

1. Authenticate and authorize the caller and collector/site binding.
2. Validate syntax, references, layout bounds and supported enum/grammar values.
3. Resolve exact revisions and ensure no incompatible or missing field mapping.
4. Verify source operation capability and source security constraints.
5. Apply source and site/tenant admission ceilings before initiating work.
6. Execute the bounded source probe where authorized.
7. Report negotiated settings and source-shape/time/quality evidence.
8. Decide activation eligibility; persist the evidence and audit outcome.

A UI “validated” badge is not trusted input. Repeat material checks on activation and runtime boundary changes. Keep existing refusal/error mechanisms; proposed semantic refusal names are not automatically allocated numeric product codes.

### 10.5 Suggested refusal categories

Examples: `source_operation_unavailable`, `field_layout_out_of_bounds`, `field_overlap_undeclared`, `source_identity_ambiguous`, `recording_rate_not_qualified`, `deadband_range_undeclared`, `trigger_capture_semantics_unavailable`, `source_position_expired`, `dataset_capacity_exceeded`, `raw_retention_dependency_blocked`, `source_schema_incompatible`.

Each result carries the offending field/setting, measured or negotiated value where relevant, remedy, operation/run identity and trace identifier. Existing codes such as OT01 retain their established meaning. New codes require allocation through the current error catalogue, not hardcoded private lists.

---

<a id="jobs"></a>
## 11. Jobs, continuous execution and downstream readiness

### 11.1 Preserve import binding

The current design deliberately binds an **import** job to `source_dataset_definition_id`; projection/analysis/ML/report jobs use their required definition target contracts. Preserve this distinction. An acquisition revision is resolved for the dataset and recorded on the actual run, without forcing import jobs to use a transformation's target slot. [P03]

One dataset can have an approved recurring ingestion job and separately authorized backfill work. Concurrency rules must prevent overlapping writers or require disjoint source windows with deterministic receipt/checkpoint coordination.

### 11.2 Finite import run

```text
Due/manual request
 -> canonical Job resolution and exact configuration binding
 -> existing admission + source-budget reservation
 -> source-consistent bounded read
 -> durable staged chunks / receipt / position
 -> finite batch completion
 -> explicit run outcome
 -> eligible downstream dependency notification
```

Preserve current no-work, partial, failed, cancelled and successful distinctions. An HTTP success response or one successful dataset does not make a mixed-failure multi-dataset run successful. A no-change rerun must not create a second payload history merely to show progress.

### 11.3 Continuous acquisition run

A continuous executor owns an approved long-running session with a lease, heartbeat, exact configuration revision, source binding and resource reservation. Its internal sample and trigger timers implement the approved acquisition plan; they do not become an alternative Job authority.

**Do not create one Job occurrence per sample or change event.** This would turn scheduling/logging metadata into a second high-volume stream and cannot express a persistent subscription cleanly.

The existing Job plane owns lifecycle, operating windows, pause/cancel, retries, fencing, placement and admission. Extend its executor capability to declare continuous execution and resource occupancy. A reserved acquisition lane is a governed admission class, not a new scheduler.

### 11.4 Session, batch and window are different identities

| Identity | Lifetime / purpose |
|---|---|
| Acquisition session | Continuous ownership/runtime context, possibly long lived. |
| Source window | A bounded consistency/retrieval boundary, when applicable. |
| Import batch | A finite accepted set of records with completion state and lineage. |
| Capture/event identity | One logical periodic occurrence, source event or trigger-associated record. |
| Delivery attempt | One network attempt; never the source fact's identity. |

Completed micro-batches may be released while the acquisition session remains active. Partial batches remain invisible to canonical projection. Where a finite source snapshot requires all chunks before use, its parent window remains the readiness barrier. These are explicit readiness policies, not one overloaded `Completed` boolean.

### 11.5 Reliable downstream handoff

Batch completion and a downstream notification/outbox entry must have durable atomic semantics. If the process fails after data commit but before sending the notification, the Job plane must eventually discover/replay that completed batch. A notification retry must not trigger duplicate projection effects.

The transformation run records exact dataset/batch/schema/configuration references and exact published transformation version. Do not implement “run against whatever is latest in Dump Store” for a reproducibility-sensitive Job.

Multi-source transformations need explicit input readiness: selected completed batches/windows, allowed staleness and maximum age, not an accidental join of the latest record from unrelated source times. Consume the current dependency freshness policy instead of inventing an OPC-specific one. [P03] [P07]

### 11.6 Cancel, restart and ownership

A cancellation request is not a completion acknowledgement. The executor stops new acquisition at a defined boundary, records already committed data, leaves uncommitted work unadvanced and reports final state once. The policy states whether the spool drains after collection is paused.

Use lease/fencing tokens to prevent two active collectors from owning the same non-parallel stream. A new owner must not cause already durable data in an old collector's journal to be silently discarded: define an authenticated replay-only handoff for that old journal, separate from permission to acquire new data. Source identity and receipt deduplication still decide record acceptance.

For buffered source events, resume by source epoch/sequence. For periodic captures with no source history, record the interruption; do not synthesize missed history. Reconnection to a current value is not event replay.

### 11.7 Dependency discipline

T-106 may be tested using registered test executors before every production executor exists. Do not add T-261 or OPC feature completion as a prerequisite to the generic scheduling authority. T-107 owns admission; T-108 owns existing delta/receipt correctness; new acquisition components consume their contracts. Separate **contract-ready** handoff from **completion prerequisite** to avoid circular delivery plans. [P07]

---

<a id="dump-store"></a>
## 12. Dump Store logical and physical design

### 12.1 Direct answers to the owner's table question

- A **customer database does not become one table**. Its selected objects become separately registered datasets.
- A **PLC DataBlock does not automatically become one physical table**. It may hold a structured record, many signals or a buffer of events, each with declared output shape.
- A **signal does not require one table** and a sample does not require one Job.
- Every dataset must be visible to preparation tools as a **typed source-shaped logical table or equivalent typed record set**, regardless of physical storage.
- All accepted source data belongs to the Dump Store boundary before canonical transformation. Data intentionally excluded by an approved acquisition policy is not falsely described as imported history.

### 12.2 Recommended physical decision, with alternatives

The reviewed design already has `import_batches`, `staging_records`, `cursor_watermarks`, `schema_drift_events` and quarantine concepts. Retain those as the baseline authority. Resolve the ambiguous `stagingTableName` wording explicitly. [P03]

| Option | Strength | Cost/risk | Disposition |
|---|---|---|---|
| One shared row/envelope store with structured payload | Reuses current generic staging; handles diverse schemas and revisions | Payload parsing, wide indexes and high-volume row overhead require measurement | Recommended bounded baseline, with partitioning and typed dataset access. |
| Typed physical table per dataset | Efficient typed queries for stable, heavily used datasets | Schema churn, DDL concurrency, tenant/table count and migration complexity | Supported only as a governed payload implementation or rebuildable read projection when justified. |
| Columnar/segment payload with database manifests | Suitable candidate for large immutable batches and high-speed records | Cross-storage durability, readers, archive lifecycle and backup complexity | Explicit advanced extension, not an unannounced prerequisite for all basic ingestion. |
| Keep full payload in several independent stores | Superficially easy consumer access | Contradicting versions, excess volume and competing writer authority | Reject. |

**Recommendation:** one authoritative payload placement per accepted dataset batch/version. Shared row storage is the initial default for supported measured workloads; typed/materialized representations are governed projections, or a declared alternative payload implementation—not a hidden second truth. The chosen payload placement and reader capability must be recorded before activation.

No assertion is made that a JSONB row store can ingest or query every proposed 1 ms workload. Benchmark before selecting physical layouts for those envelopes.

### 12.3 Logical schema responsibilities

| Schema / existing family | Required extension or use |
|---|---|
| `ppiq_meta.connection_profiles` | Provider-aware access references and collector binding; retain one source access authority. |
| `ppiq_meta.source_dataset_definitions` | Source selector kind, stable dataset identity, current approved schema/acquisition/storage policy references. |
| `ppiq_meta.source_field_definitions` | Stable field ID, source locator, technical/display names, typed layout and immutable revision association. |
| Existing definition lifecycle | Registered acquisition/layout configuration revisions and dependencies, where the accepted kind model permits; explicit extension if it does not. |
| Existing Job definitions/history | Dataset binding, resolved acquisition revision and runtime/session/occurrence evidence. |
| `ppiq_staging.import_batches` | Finite batches, source window/session link, schema/configuration references, durable status and manifest. |
| `ppiq_staging.staging_records` | Accepted source records with typed payload/envelope and source operation/identity. |
| `ppiq_staging.cursor_watermarks` | Existing source position authority extended to supported source-position kinds. |
| Receipt identity family | Reuse or extend the current receipt mechanism; do not create another independent dedup/cursor authority. |
| Existing quarantine/drift families | Parse/layout/schema issues and reprocessing evidence, distinct from intentional filtering. |
| `ppiq_plant` canonical facts | Existing governed projection targets; no direct adapter writes. |

Table names for genuinely new physical entities must be assigned through the catalogue/naming authority. The table above is not executable DDL and does not authorize mass renames or suffix-based schema generations.

### 12.4 Minimum batch and record contracts

```text
Batch:
  tenant_id, dataset_id, batch_id
  acquisition_session_id?, source_window_id?
  schema_revision_id, acquisition_revision_id, adapter_build_id
  source_position_from/to, source_epoch?
  record_count, payload_bytes, payload_hash/manifest
  payload_placement, state, committed_at
  predecessor/continuation references and completeness policy

Record envelope:
  record_id, tenant_id, dataset_id, batch_id, logical_record_index
  source_record_identity?, source_revision_or_position?, operation_kind
  capture_id?, trigger_event_id?, source_epoch?, source_event_sequence?
  source_time?, server_time?, captured_at?, ingested_at
  time_basis/provenance, quality/status evidence
  source-shaped typed payload or bounded payload reference
```

Not every provider can supply every field. Unknown source time/identity remains explicitly unknown; it is not filled with an invented timestamp or key. Locally assigned stable observation IDs support delivery retries but cannot prove deduplication of source events whose identities were never supplied.

### 12.5 Record shape

A SQL source row stays a record, not one staging row per column. A file row carries file/selector/record provenance. A sparse on-change output stores changed fields with their own event/time/quality context. A periodic group or triggered snapshot can be one structured record with member evidence. A source buffer becomes newly identified contained events according to its contract.

Do not force every source into an EAV “one field per row” layout, and do not force unrelated asynchronous signals into a wide row that falsely implies simultaneous observation. The selected record shape is part of the acquisition revision and capacity estimate.

### 12.6 Typed payload and byte fidelity

PostgreSQL JSONB does not preserve original whitespace, key order or duplicate keys; it is therefore not a byte-for-byte archive of a file or protocol message. [E21]

Define a typed encoding for integers beyond safe JavaScript precision, decimals, binary values, timestamps/offsets, arrays, null/missing and non-finite floats. The frontend preview must not round-trip precision-sensitive values through an imprecise number representation. A failed cast is a typed issue, not a silent null or zero.

When original bytes must be retained, store a content-addressed/source-scoped artifact with checksum, access policy, retention and restore evidence. Link it to the accepted batch. Original bytes, parsed source rows and canonical facts have different purposes and lifetimes. Do not call JSONB “verbatim raw bytes.”

An artifact-backed authoritative payload needs an explicit durable commit protocol: upload immutable object, verify it, atomically register its manifest/receipt/checkpoint, and publish the batch only after all required references are durable. Orphan objects are garbage-collected under a safe grace rule; a missing object must never be hidden behind a successful manifest.

### 12.7 Dataset access for Canvas and safe SQL

The schema/table bar resolves logical dataset and field IDs to a reader capable of the chosen storage representation. The compiled plan preserves source types, dataset revisions, authorized tenant and completed-batch boundaries.

For row/JSONB storage, a typed projection or secured view can expose source-shaped columns. For a typed physical payload, the reader resolves the registered relation. For an explicitly supported archived segment, it resolves the manifest and archive reader or initiates a governed restore. Unsupported cold reads are visibly unavailable; they do not silently return empty data.

The SQL authoring path must remain usable, not just the preview grid. Compile registered dataset references to approved storage reads; parameterize values and validate identifiers/expressions. A view/service that omits tenant scoping or completed-batch filtering is not an acceptable shortcut.

No analytical dashboard bypasses canonical Mapping/Canvas by reading Dump Store directly. Bounded administrative preview remains permission-scoped source inspection, not business analysis.

### 12.8 Keys, transactions and partitioning

A primary-key shape must distinguish record identity from storage location. For partitioned record data, include the partition key where PostgreSQL requires it in a unique/primary constraint, or use a separate receipt authority with an enforceable global identity. Do not write a proposed `UNIQUE(record_id)` over a time-partitioned table and assume it is valid. [E22]

All foreign-key relationships requiring tenant ownership use tenant-aware references or equivalent enforceable consistency. A foreign dataset ID in a valid-looking batch is refused before data access.

Maintain durable receipt identity for at least the supported retry/replay/reimport horizon. Expiring it too early permits old deliveries to be accepted again. Payload retention and receipt retention are different controls. A payload hash mismatch for an already accepted source identity/revision is an integrity conflict, not an idempotent success.

Use batched writes/bulk interfaces only within the same data/receipt/checkpoint transaction boundary. A faster bulk path that bypasses tenant enforcement or leaves half a committed receipt is not an optimization.

### 12.9 Source history versus current state

Dump Store preserves the accepted source revision evidence required by its mode. A derived latest-state table/view may update in place. A genuine source update is not a retry, and a delete event is not permission to destroy all prior raw evidence immediately.

For periodic records, equal value/time-at-source may still produce distinct legitimate capture identities. Deduplicating `(value, source_timestamp)` would erase intended periodic history. For source events, identity includes event/version semantics; do not replace it with the delivery batch ID.

---

<a id="capacity"></a>
## 13. Capacity, retention and recovery

### 13.1 Capacity is a vector, not “number of tags”

Measure and constrain source requests/s, bytes/read, source scan/CPU impact, active sessions/subscriptions, monitored items, qualifying events/s, values/s, record widths, payload bytes/s, batch overhead, spool bytes, core write/WAL load, projection throughput and storage lifetime.

An installation can be limited by PLC load, network, collector CPU, WAL, indexes, archive throughput or projection backlog before the raw data volume fills the database. Source read-only credentials do not remove these constraints.

Chapter 6 already defines capacity profiles and calibrated/reference constants. Extend those profiles; do not announce a new unmeasured capacity band. Existing “rows/s” bands cannot be treated as “signals/s” or “PLC messages/s” without knowing record shape and width. [P06]

### 13.2 Periodic growth arithmetic

For `N` scalar values captured each period `T_ms`:

```text
values_per_second = N * 1000 / T_ms
values_per_day    = values_per_second * 86400
```

For 1,000 selected scalar values, recording every acquisition:

| Period | Values/second | Values/minute | Values/day |
|---|---:|---:|---:|
| 1 ms | 1,000,000 | 60,000,000 | 86,400,000,000 |
| 100 ms | 10,000 | 600,000 | 864,000,000 |
| 250 ms | 4,000 | 240,000 | 345,600,000 |
| 500 ms | 2,000 | 120,000 | 172,800,000 |
| 1 second | 1,000 | 60,000 | 86,400,000 |

These are arithmetic values, not necessarily rows, messages, network requests or physical bytes. Record grouping and compression change storage behavior; they do not make the information volume disappear. Batch writes reduce transaction overhead, not the number of accepted values.

On-change traffic can approach the monitored rate when values remain noisy. Trigger traffic depends on event rate and capture width. The preview must show measured or assumed distributions and a burst ceiling, not claim on-change is inherently small.

### 13.3 Storage accounting

```text
hot_storage = current payload retained
            + new payload over each dataset retention window
            + indexes / TOAST / receipts / manifests / operational metadata
            + current-state and governed derived data

volume_reserve = WAL + temporary work + maintenance headroom
               + co-located spool / backups / restore workspace where applicable

archive_storage = archived payloads + immutable versions + manifests
                + required replicas/backups and retrieval reserve
```

Use measured encoded bytes per record and measured index/WAL amplification for the supported profile. Do not multiply by an invented “standard compression ratio.” Explain decimal GB versus binary GiB consistently. PostgreSQL exposes separate table and total-relation size measurements; operational disk capacity is broader than one table-size query. [E25]

### 13.4 Source and tenant admission

Admission runs before activation, backfill and material configuration changes. It combines per-dataset demand with the already reserved source/collector/site/tenant load. A child policy may tighten but not bypass a parent ceiling.

Reserve live acquisition capacity separately from backfill/analysis workload through the existing admission authority. At overload, use the declared response: reject new activation, pause lower-priority backfill, buffer within the agreed bound, or record explicit collection loss where the source cannot be backpressured. Do not silently change a 100 ms recording policy to 1 second.

Estimated time to capacity is useful but conditional. A finite disk cannot promise unlimited history or indefinite outage tolerance. PostgreSQL's disk-full behavior makes reserved headroom an availability requirement, not optional cosmetic telemetry. [E24]

### 13.5 Spool and catch-up

For an illustrative constant accepted byte rate `lambda`, an outage duration `D` requires at least `lambda * D` payload storage, plus journal overhead, bursts and reserve. Catch-up while live traffic continues requires drain capacity `mu > lambda`:

```text
catch_up_seconds = backlog_bytes / (mu - lambda)
```

The reference example at 250,000 bytes/s for one hour produces 900 MB of payload. A 750,000 bytes/s delivery path has 500,000 bytes/s spare capacity and needs 1,800 seconds to drain it while live input continues. This is a formula example, not a performance measurement.

The source buffer may have a shorter horizon than the edge spool. Source-to-edge loss cannot be repaired by an edge journal that never received the data. Source CDC positions also have retention limits; a lagging consumer can exhaust source log storage or lose its valid resume boundary. [E19] [E20]

### 13.6 Dataset lifecycle, distinct from log retention

| Data family | Recommended lifecycle principle |
|---|---|
| Edge journal | Retain until durable core acknowledgement plus the configured local recovery margin. |
| Parsed source payload | Retain for the agreed replay, dispute and downstream dependency window; archive or reclaim only when safe. |
| Original bytes | Policy-dependent evidence retention; link parser/layout versions and integrity checks. |
| Batch manifests/receipts | Outlive any payload or canonical evidence that depends on them, and the supported replay horizon. |
| Canonical facts | Governed business/history retention; do not shorten merely because a raw staging budget is low. |
| Current-state projection | Replaceable/rebuildable state, separate from historical evidence. |
| Feature/aggregate/model artifacts | Existing domain-specific retention and reproducibility rules. |
| Audit and operational logs | Existing log policies, including T-199 when implemented; not the source-data retention authority. |

For source-data cleanup, require impact preview, permission, dependency floor, archive verification where required, legal/operational hold support as applicable, completed-batch checks, bounded deletion and reconciliation counts. A failed archive leaves source evidence intact. Restoring metadata without its required payload artifacts is not a complete restore.

Raw-data retention must not be silently lower than the unprocessed projection/reprocessing horizon. A growing downstream backlog therefore blocks deletion or produces an explicit storage/admission decision; it does not authorize data destruction to keep a health badge green.

### 13.7 High-speed path

For workloads genuinely requiring sub-millisecond/millisecond evidence, prefer a qualified source-local acquisition/buffer mechanism that produces identified timed blocks when ordinary OPC sampling cannot meet the requirement. This is a different supported acquisition profile, not a front-end number change. Beckhoff oversampling is an example of source-side timed multi-sample transport, not proof that all OPC servers behave that way. [E14]

If the required throughput exceeds the certified Dump Store layout, require an approved segment/typed-storage profile or refuse activation. Do not silently downsample data the user requested to retain. Downsampled/aggregated data is a separate derived product with declared semantics and provenance.

---

<a id="security"></a>
## 14. Security, deployment and operational acceptance

### 14.1 Security controls

- Authenticate collectors as scoped service identities; bind them to allowed tenants/sites/sources and capabilities.
- Verify application trust and user authorization separately for OPC. No automatic trust-all or security downgrade. Protect private keys, rotate credentials and record trust changes. [E27]
- Validate source addresses, endpoints and file locations against approved policies to prevent arbitrary network/file access through configuration.
- Use source-specific read permissions and budgets. A SQL text beginning with SELECT is not by itself proof of side-effect-free execution; approved statements/functions and provider privileges must constrain the path.
- Enforce tenant identity on records, receipts, metadata, raw previews, generated views and artifact reads. Database owners/superusers and `BYPASSRLS` behavior require deliberate role design; RLS is not a substitute for secure runtime identities. [E23]
- Bound message size, recursion, arrays, string lengths, decompression and decoding work; quarantine malformed payloads without crashing the collector.
- Publish signed/versioned deployment and configuration artifacts, constrain the configuration grammar and prevent arbitrary remote scripts from being smuggled into an “advanced decoder.”

ISA/IEC 62443's zones/conduits and risk-oriented approach provide a relevant OT segmentation framework. This report is not a compliance assessment against the full standard and does not certify the installation. [E28]

### 14.2 Deployment and upgrade

Document the exact collector/adapter/SDK versions, supported source profiles, secrets location, resource budgets, startup identity and journal path. Pin dependencies and review license/security updates. The official OPC .NET stack is an implementation candidate; merely adopting it is not product interoperability certification. [E29]

An upgrade must preserve source positions, receipts, field/layout references, journal format compatibility and active configuration bindings. Drain or explicitly hand over ownership. Test failure after partial upgrade and rollback without replaying accepted history as new.

A new decoder revision must not reinterpret an existing durable journal under a different layout accidentally. Replay uses the recorded revision; explicit reprocessing creates a separately identified derivation under the selected new mapping/decoder policy.

### 14.3 Health model

Expose separate connection, acquisition, durability, projection and capacity states. Examples:

```text
Connection: Connected
Acquisition: Running / source gap detected
Local journal: Durable, 42% used
Core delivery: Retrying / last durable acknowledgement at ...
Dump Store: Last completed batch ...
Projection: Quarantined rows / backlog age ...
Capacity: Within envelope / activation blocked / reserve warning
```

A live transport keepalive does not prove a signal is updating. A source that produces no new events is not automatically failed. A successful acquisition with a failed projection must not be displayed as a wholly healthy end-to-end journey.

Metrics should include source requests/latency/errors, observed versus admitted records, filter counts, queue overflow, sequence gaps, decoder failures, local/core durable positions, bytes and rates, journal age/depth, capacity reserve, projection backlog and schema/configuration versions. Routine logs are batched or summarized; bounded diagnostic sessions can expose individual fields.

---

<a id="validation"></a>
## 15. Validation plan and executed reference models

### 15.1 What qualifies as acceptance

The acceptance levels are separate:

1. **Contract tests:** deterministic semantic/grammar/type behavior.
2. **Adapter tests:** real protocol implementation against a qualified test source/server.
3. **Product integration:** actual configuration, Jobs, Dump Store, projection, tenant isolation and runtime outcomes.
4. **Site commissioning:** actual customer's source identity, permissions, layout, consistency and access constraints.
5. **Workload/endurance certification:** declared source/collector/database/storage envelope under faults and concurrent load.

A prototype screenshot, test-double result, passing parser or label-presence script cannot substitute for levels 2-5. Existing task closure evidence applies only to the scope it actually proved.

### 15.2 Proposed acceptance matrix

| ID | Scenario | Required observable outcome |
|---|---|---|
| V01 | Create each named provider profile | Correct provider-specific fields; unavailable operations remain unavailable. |
| V02 | Draft source configuration with no activation | No background ingestion starts. |
| V03 | Import 1,000 field definitions | Bounded responsive editing, errors identified by row/field, no automatic fast rate. |
| V04 | Rename a display label | Field/job/mapping identity unchanged; historical interpretation preserved. |
| V05 | Change byte offset/type | New revision, impact shown, old batches retain old layout. |
| V06 | Invalid width/offset/string/array bound | Refused before reading or decoding beyond buffer bounds. |
| V07 | Overlapping raw fields | Undeclared overlap refused; declared bitfield overlay correctly decoded. |
| V08 | Raw, ByteString and typed OPC input | Exactly one correct decode path; no double byte-order/scale conversion. |
| V09 | Optimized/unexposed DB address | Typed unsupported/refused result; no fabricated source access. |
| V10 | Periodic constant signal | Correct logical record count and truthful capture/source timestamps. |
| V11 | Scheduler late/missed ticks | Gaps/lateness explicit; no retroactive latest-value replication. |
| V12 | Cached stale or disconnected signal | Freshness policy applied; no fabricated new Good measurement. |
| V13 | Tiny representable change, no deadband | Preserved through encode, journal, ingest and preview without hidden rounding. |
| V14 | Change below/equal/above band | Exact declared boundary and cumulative baseline behavior. |
| V15 | Bad/Uncertain/Good transitions | Status preserved independently of numeric band. |
| V16 | Percent band without engineering range | Configuration refused, not inferred from observed sample extrema. |
| V17 | Upstream/downstream filter combination | Equivalence demonstrated or semantic difference explicitly refused/disclosed. |
| V18 | One changed field in a large group | Only configured changed-field output, unless whole-group capture selected. |
| V19 | Rising/falling edge with sustained level | Exactly the declared capture count and re-arm behavior. |
| V20 | Startup/reconnect while trigger true | Initial-state policy, not invented edge or duplicate event. |
| V21 | Short pulse between scans | No completeness claim; source-latching requirement or unobservable-event limitation shown. |
| V22 | Counter jump/reset/wrap | Correct classification and gap evidence; no invented missing payloads. |
| V23 | Trigger arrives faster than capture completion | Bounded queue/overrun policy; no silently reassigned snapshot. |
| V24 | Source-latched record modified mid-read | Integrity/consistency failure or bounded retry; no false atomicity. |
| V25 | Equal timestamps but mixed cycle IDs | Refused/marked inconsistent despite small skew. |
| V26 | Old source-change timestamp, valid unchanged field | Not declared stale solely on that timestamp when source validity is established. |
| V27 | Buffered records and slot reuse | New source events only, replay dedupe, overflow/reset detected. |
| V28 | AckRequired means process-memory write | Operation unavailable under read-only contract; existing production consumer unaffected. |
| V29 | Requested rate/queue revised by server | Requested/revised/effective evidence distinct; incompatible plan not silently activated. |
| V30 | Queue one and intermediate changes | Latest-only behavior disclosed; not certified as complete event history. |
| V31 | SQL equal-watermark records span pages | All records retained with stable provider-native ordering, including restart. |
| V32 | Source update/delete/concurrent page changes | Supported source-consistency strategy; no tie-break-only completeness claim. |
| V33 | CDC retention expired/source restore | Gap/reinitialization state and operator path; never silent cursor jump. |
| V34 | Same source identity/revision retried | One durable accepted effect. |
| V35 | Same business key, new revision; A->B->A | Legitimate revisions retained. |
| V36 | CSV quoted multiline/encoding/header cases | Exact declared parsing and logical record locations. |
| V37 | Same name new file; renamed same delivery | Correct source occurrence/version semantics, not path-only dedupe. |
| V38 | Truncated/replaced growing file | Resume refused/reconciled; no byte-offset corruption. |
| V39 | Excel formulas/macros/external links | Defined data-only behavior; no unintended execution. |
| V40 | Decoder/parser malformed/oversized input | Bounded resource use, quarantine and useful diagnostics. |
| V41 | Crash before/after local journal commit | Durable guarantee begins at documented point; no baseline/checkpoint advance beyond durable data. |
| V42 | Core commit succeeds but acknowledgement lost | Resend same identity; no duplicate payload effect. |
| V43 | Crash after batch commit before dependency notification | Durable outbox/recovery releases batch exactly as governed. |
| V44 | Endless subscription with completed batches | Projection runs on eligible finite batches while session remains active. |
| V45 | Parent source snapshot only partly staged | Downstream does not consume incomplete parent window. |
| V46 | Cancel and racing owners/failover | One terminal outcome; no split-brain acquisition; accepted old journal safely reconciled. |
| V47 | Cross-tenant IDs and raw previews | No data, metadata, existence or artifact leakage. |
| V48 | Dataset exposed to Canvas and SQL | Same typed authorized fields and batch boundaries across both representations. |
| V49 | Same fact from OPC and customer DB | Source evidence preserved; governed canonical identity prevents double counting. |
| V50 | Retention while projection/replay dependency exists | Cleanup blocked or safely archived with dependency evidence. |
| V51 | Archive upload/verification/restore failure | No premature payload deletion; operator-visible recovery. |
| V52 | Receipt expires before possible retry | Configuration rejected or explicit replay boundary enforced; no silent duplicate resurrection. |
| V53 | Aggregate site load exceeds limits | New work refused/queued per policy even when each source is individually valid. |
| V54 | Spool or database reserve exhausted | Defined loss/backpressure/pause behavior, alarms and honest gap evidence. |
| V55 | Source CDC slot/log pressure | Source health protected by approved limits and recovery procedure. |
| V56 | Concurrent backfill, BI and live acquisition | Reserved capacity and published latency/budget behavior demonstrated. |
| V57 | Fresh install and second industry fixture | No example-specific fields/tables/rates required; no customer product-code diff. |
| V58 | Collector/schema/configuration upgrade and rollback | Revision-safe journal replay, checkpoints, identities and historical interpretation. |
| V59 | 1 ms requested at declared field/payload count | Pass only with measured end-to-end envelope; otherwise refuse the claim. |
| V60 | Finite/current/archived data reconciliation | Counts/identities/bytes and lineage reconcile across lifecycle transitions. |

### 15.3 Executed reference models

**Result: 37/37 reference-model checks passed.** They use Python standard-library arithmetic, small in-memory semantic models, datatype encoding and CSV parsing. They do not import PPIQ code, contact a source, run PostgreSQL or establish an operating-system timing guarantee.

The checks cover five periodic volume calculations; strict/cumulative/zero-band fixtures; constant periodic logical ticks; edge startup/re-arm examples; pulse observability and counter ambiguity examples; example layout bounds and relative offsets; Float32 encoding; a five-page composite cursor fixture; a watermark-only counterexample; source revision/tenant/epoch receipt identities; atomic commit/failure models; spool/drain formulas; and complete example-CSV parsing/conditional arithmetic.

Some checks are intentionally arithmetic counterexamples or invariants, not fault-injected implementations. Their purpose is to make design assumptions falsifiable; they are not substitutes for V01-V60. The detailed executed result names are listed in Appendix B.

---
<a id="part-ii"></a>
# Part II — Recommendations for the design chapters

<a id="chapter-register"></a>
## 16. Chapter-by-chapter change register

### 16.1 Authority-preserving approach

Do not add this report to the product as an independent set of mandatory rules. Approve decisions, insert their operative clauses in the owning chapters, align the derived illustrations, then update the backlog. This report becomes the rationale/evidence for those changes, not a standing override.

The following references identify sections actually present in retrieved documents or explicitly referenced by the current Chapter 3/backlog. **Chapter 4's full current body must be reconciled at merge; the older mounted text is historical structure only.** The review does not infer absent behavior merely from a failed search.

| Chapter | Owning sections / surfaces | Recommended change | Nature |
|---|---|---|---|
| **1 — Marketing and Sales** | §1.0.1 dataset-neutral promise; §1.7 limits; §1.8 objections; §1.10 demonstration; §1.11 traceability | State configuration-driven multi-source acquisition without universal PLC-driver, rate, losslessness or capacity claims. Separate implemented, site-commissioned and workload-qualified claims. | Clarification of commercial truth plus traceable new approved promises. [P01] |
| **2 — Technical Overview** | §2.0.3 genericity; §2.0.9 acquisition boundary; journey/DF overview; glossary §3.9 and relevant catalogue | Add Connection/Dataset/Field/Layout/Recording Group/Session/Batch distinctions and three recording modes. Preserve one journey and downstream canonical boundary. | Product-level extension, not endpoint/schema details. [P02] |
| **3 — General Technical Function Description** | DF1-DF3; B1-B5; §4.5.3 staging; §4.5.4 observations; §4.5.5 fields/config; §4.5.5a Jobs; §4.5.11 lifecycle; §§4.5.20-21 catalogue/errors; §4.6 security/topology | Main industrial acquisition contract: field layouts, capture semantics, APIs, storage identity, UI state, precision and authoritative physical/logical storage decision. | Material functional engineering extension plus specific ambiguity/reference corrections. [P03] |
| **4 — Specific Technical Function Description** | Current references to §5.3.2a scheduling and §5.3.6 dependency freshness; relevant §5.2 authoring/schema/SQL and §5.3.7-9 execution/progress/delta sections | Define finite vs continuous execution, session/batch/window readiness and dataset-reader integration. Preserve schedule/loop authority and existing semantic engines. | Execution integration extension; current full-body reconciliation required. [P04] [P03] [P07] |
| **5 — Tutorial/User Journey** | §6.0.0; existing T1/T2/T3/T4/T7 tutorials and troubleshooting/glossary | Add source-family and DataBlock-layout variants, recording configuration, capacity refusal, jobs/recovery and dataset preview within existing tutorials. | Tutorial consequences of approved Ch3/4 behavior; no independent semantics. [P05] |
| **6 — Infrastructure/Administration** | §6.1.1.4 OPC boundary; deployment/upgrade; §6.1.5.8 C1-C4; §6.1.9 sizing; §6.1.11 backup; §6.1.12 observability; website/sales trace | Specify adapter/collector packaging, resource dimensions, outage/recovery, storage lifecycle implementation and combined workload certification. | Operational extension; retain and expand existing capacity/backup framework. [P06] |

### 16.2 Proposed amendments by concern

| Change ID | Exact concern | Primary owner | Other chapters consume |
|---|---|---|---|
| DC-CONFIG | One source configuration journey with provider-aware forms and revisions | Ch3 DF1/DF2, B1-B3 | Ch2, Ch5, Ch6 |
| DC-FIELD | Stable field identity, raw layout, naming and decoder version | Ch3 DF2 / field catalogue | Ch4 authoring, Ch5 |
| DC-CAPTURE | Periodic/on-change/triggered contract, modifiers and output shape | Ch3 acquisition extension within DF1-DF3 | Ch2 overview, Ch4 execution, Ch5 tutorial |
| DC-TIME | Timing/freshness/quality/precision and event consistency | Ch3 observation/source-time contracts | Ch4 analytics eligibility, Ch6 qualification |
| DC-JOB | Continuous executor, lease, session and bounded batch completion | Ch4 Job execution contract | Ch3 APIs/state/persistence, Ch5, Ch6 |
| DC-DUMP | Logical dataset versus physical store; receipt and payload identity | Ch3 §4.5.3 | Ch4 schema bar/SQL/executor, Ch6 storage |
| DC-PROVIDER | SQL/file incremental/revision/parse/resume semantics | Ch3 DF2/DF3 | Ch4 Jobs, Ch5, Ch6 |
| DC-LIFECYCLE | Dataset retention, archive, dependency floors and quota impact | Ch3 configuration/persistence semantics | Ch4 consumer readiness, Ch6 operations |
| DC-CAPACITY | Source/collector/site/tenant aggregate envelope | Ch6 sizing/certification, with Ch3 activation refusal | Ch1 claims, Ch2 product truth, Ch4 admission |
| DC-CORRECTIONS | Chapter 7 reference, existing staging ambiguity, imprecise timing claims | Owning clause where each appears | Derived tutorial/visual/backlog references |

---

<a id="chapter-clauses"></a>
## 17. Proposed chapter clauses and detailed insertion specifications

### 17.1 Chapter 1 — bounded product promises

**Recommended insertion under dataset-neutral integration/claim limits:**

> PlantProcess IQ connects selected industrial and business data through customer-configured, read-only interfaces. Customers define source objects, field identities and supported layouts, acquisition policies, storage limits and Jobs without customer-specific product code. The product distinguishes implemented connector capabilities from the source operations configured and commissioned at a site.
>
> Recording may be periodic, change-qualified or trigger-qualified where the supported source profile can deliver the requested semantics. Requested timing, source timing and achieved workload performance are disclosed separately. The product does not promise universal native-PLC compatibility, capture of unobserved physical events, atomic source snapshots or unlimited retention merely because a setting is available.

**Traceability action:** add each accepted new commercial outcome to §1.11 with its Chapter 3/4/6 owners and release acceptance. Keep the existing demonstration disclosure: a simulated source is not a customer plant connection. Do not add a “lossless at any rate” or “no source impact” claim.

**Acceptance:** the sales/website/connector capability summary reads the same approved capability truth; example data is labelled; technical qualification evidence accompanies any rate/storage promise.

### 17.2 Chapter 2 — product-level model and glossary

**Recommended extension of the acquisition boundary:**

> A source interface identifies an approved connection and execution location. A connection can supply multiple datasets. Each dataset describes selected source records and stable fields; raw DataBlock inputs additionally require a versioned field layout. Names and meanings are authored from customer evidence, never inferred from a prepared industry fixture.
>
> Recording policies expose Time, Value change and Trigger/counter modes. These policies are separate from protocol sampling/publishing and physical storage flush. Their implementation runs through the governed Job system and source-side collector. All accepted source-shaped records enter Dump Store before approved canonical projection.

**Glossary additions:** source locator, field ID, field key, display label, layout revision, capture group, source event identity, acquisition session, completed import batch, logical dataset relation, durable acceptance, requested/negotiated/measured rate, source gap, intentional filtering.

**Clarify exclusions:** write/control paths remain excluded. A protocol acknowledgement is not automatically a process write; a PLC memory acknowledgement may be. Define the boundary by behavior, not by the word “ack.”

**Journey action:** preserve J4-J9/DF1-DF5 progression. Provider-specific variations are configurations inside it, not parallel OPC and database products.

### 17.3 Chapter 3 — DF1 extension: provider-aware connection and source proof

The following eleven-field specification is proposed **inside the existing DF1**, not as a new top-level DF number:

| Required field | Proposed contract |
|---|---|
| CONCEPT | Establish an approved source path and distinguish connection validation from executable source operations. |
| ACTOR | Authorized administrator/data engineer; executing collector service identity. |
| PRECONDITION | Active tenant/site authorization, collector binding, supported provider profile and secret/trust references. |
| SURFACE | Existing B1 Connections, with provider-specific form and operation evidence panel. |
| SEQUENCE | Select provider/collector -> enter access references -> apply source budget -> run bounded test -> inspect supported operations and refused layers -> save draft -> activate only after downstream configuration gates. |
| CALLS | Existing connection catalogue/create/update/test/budget families; source tests dispatched through the collector. |
| PAYLOAD | Provider-discriminated access settings, secret references, source/site identity, requested operations and budgets; test result includes operation, failure layer, actual source identity and evidence timestamp. |
| PERSISTS | Existing connection/profile/audit authorities; test/binding evidence separate from immutable configuration content. |
| VALIDATION | Server-side authorization, allowed endpoint/location, provider schema, read-only capability, trust and source-budget checks. |
| FAILURE | Typed network/auth/trust/permission/capability/budget failure; no fake reachability or write test against production. |
| ACCEPTANCE | Two dissimilar providers configured through the same experience; operations proven independently; masked secrets; source inaccessible produces honest failure; no core direct OT connection. |

### 17.4 Chapter 3 — DF2 extension: field layouts and acquisition configuration

| Required field | Proposed contract |
|---|---|
| CONCEPT | Register source-shaped data and describe how selected fields are decoded, named and captured. |
| ACTOR | Data engineer authors; authorized publisher approves material changes. |
| PRECONDITION | Valid source profile; selected object; known or explicitly unresolved source schema/layout. |
| SURFACE | B2 Dataset Registry and B3 preparation/configuration, including the field grid and recording-group editor. |
| SEQUENCE | Select object -> discover/import field metadata -> declare stable fields/layout -> preview bounded values -> define record identity/time roles -> choose recording mode and capture strategy -> select retention -> review capacity -> validate -> publish/activate exact revision. |
| CALLS | Existing datasets/fields/business-key/cursor APIs plus acquisition/layout/retention/capacity validation operations. |
| PAYLOAD | Dataset selector kind, field IDs/keys/labels/source locators/types/layout, source identity/revision strategy, recording group references, timing/quality policies and capacity/retention references. |
| PERSISTS | Existing dataset/field definitions and common immutable lifecycle; no independent tag schema authority. Runtime revisions/resolved bindings recorded separately. |
| VALIDATION | Bounds, types, overlapping members, uniqueness, source capability, timing, deadband range, trigger state grammar, source consistency, unsupported positions and aggregate budgets. |
| FAILURE | Unknown layout/identity is explicit; unsupported rates cannot activate; drift or address changes require revalidation; no silent field-name/type inference. |
| ACCEPTANCE | Anonymous DB fields can be named and used as capture/trigger members; bulk edit works; rename preserves references; invalid layout refuses; all three modes survive reload/version round-trip and bind to a Job. |

**Additional schema clauses:**

> `source_field_definitions` must provide stable identities independent of display names and physical source addresses. Published layout revisions retain exact decoding semantics and source provenance. Changing a source address, type, range/array layout or capture role requires impact validation; no historical payload is reinterpreted implicitly.
>
> Provider selectors are typed. Database table, worksheet/range, file feed, UA node group and raw block/message selectors must not be forced into a misleading universal source-table string.

### 17.5 Chapter 3 — DF3 extension: accepted records and durable handoff

| Required field | Proposed contract |
|---|---|
| CONCEPT | Accept selected source records durably, preserving identity and source semantics while supporting finite and continuous acquisition. |
| ACTOR | Governed import/acquisition executor and the one ingestion writer. |
| PRECONDITION | Approved exact configuration, active Job/lease, sufficient capacity and valid source resume boundary. |
| SURFACE | B4 Importing, B5 Jobs Monitor and G6 progress, with session/batch/position distinctions. |
| SEQUENCE | Read/decode -> qualify/assemble -> journal durably -> deliver batch -> validate identity/integrity -> commit records/receipt/checkpoint/batch event -> acknowledge -> release eligible downstream work. |
| CALLS | Existing import/run/backfill/batch/status families and authenticated collector delivery/status operations. |
| PAYLOAD | Finite batch envelope and source-shaped records with schema/configuration/adapter versions, position/time/quality and capture/event identity. |
| PERSISTS | Existing staging/batch/cursor authorities, receipt integration and reliable completed-batch notification; local journal is an explicit deployment component. |
| VALIDATION | Tenant/source binding, integrity, duplicate identity vs new revision, source window completeness, maximum bytes/records, schema and source position. |
| FAILURE | No cursor advance on uncommitted work; no duplicate on lost ACK; source gaps/overflow and downstream quarantine are distinct; partial windows remain blocked. |
| ACCEPTANCE | Retry and restart preserve identities; new revisions survive; completed micro-batches project before a continuous session ends; crashes at commit/notification boundaries recover correctly. |

**Keep the existing cursor law intact.** Extend its source-position kinds and evidence; do not weaken ordered ties, late-data handling or atomic staging to accommodate an adapter shortcut.

### 17.6 Chapter 3 — recording semantics to insert

> The customer selects periodic, on-change or triggered recording per registered output group. Source observation, recording qualification, record assembly and durable storage are separate contract dimensions. The execution plan records the exact field/layout revisions, comparison basis, time/quality policy and supported source mechanism.
>
> Periodic records identify the capture occurrence and retain original source timing. On-change records use the declared threshold/equality policy and cannot hide an additional source or downstream filter. Triggered records identify their trigger and capture strategy; cached, read-after-trigger and source-latched/event captures are not equivalent. Missing counter increments or missed periodic captures are not synthesized from later values.
>
> Published requested settings are immutable. Negotiated source parameters and measured performance are runtime evidence. An unsupported or unqualified setting refuses activation, rather than silently changing the approved acquisition meaning.

**Time/quality insertion:** preserve original StatusCode/type/time precision and source timestamp roles. Canonical time selection consumes Source Time Authority. A capture-time field must not masquerade as device event time. Source precision limitations and any canonical precision reduction remain inspectable.

### 17.7 Chapter 3 — Dump Store adjudication

**Replace ambiguity, not blindly add another store:**

> Registration creates a logical typed dataset identity. Its source name is not a direct physical PostgreSQL identifier. A dataset is exposed to preparation through a governed reader resolving the approved payload placement, schema revision, tenant scope and completed-batch boundary.
>
> The default payload path uses the existing generic staging/batch/receipt authority for its qualified workload. Any typed physical representation or artifact-backed payload must declare whether it is authoritative or a rebuildable projection. One accepted batch has one authoritative payload placement; parallel independent writers for the same raw fact are forbidden.
>
> Original-byte fidelity and parsed source-row fidelity are separate. A retained original file/protocol artifact has integrity, authorization, retention and restore evidence. JSONB source rows alone are not a byte-for-byte source archive.

Update DF2's `stagingTableName` result to a logical dataset relation descriptor, or explicitly specify its compatibility behavior. Do not remove existing clients' fields without a controlled wire migration. Update the schema/table bar and SQL compiler to consume that descriptor.

Extend §4.5.3 with session/window/completeness relationships, stable record/source-revision identities, immutable layout references, delivery receipts and finite retention requirements. Amend keys/FKs/partitions through the existing catalogue and tenant rules.

**Correct the “Chapter 7” retention pointer** to the accepted source-data policy owner and Chapter 6 operational implementation. A reference correction alone does not implement retention.

### 17.8 Chapter 3 — Jobs/lifecycle/UI/security integration

Retain §4.5.5a's import-dataset exception. Add exact acquisition revision resolution and session metadata without replacing transformation target binding. Extend the approved definition-kind model only through the existing lifecycle owner.

Update B1-B5 using the ten-field contract in Section 6.6, including field/grid controls and the explicit state matrix. Update errors via §4.5.21 and physical classification via §4.5.20. Reuse current Source Time Authority, roles, concurrency, translations and safe-action components.

State how the collector receives approved configuration without core OT connectivity. Separate source credentials from user-facing configuration and prohibit arbitrary code/SQL/control writes as configuration payloads.

### 17.9 Chapter 4 — Job execution and preparation consumers

**Current-body limitation:** the exact final wording must be reconciled against the current full Chapter 4 before applying these changes. Current §5.3.2a and §5.3.6 ownership is corroborated by Chapter 3/backlog; other cited §5.2/5.3 structural anchors were also present in the older available body. [P04]

**Proposed execution clause:**

> Import/acquisition executors declare finite or continuous execution capabilities through the existing Job registry. The Job authority owns schedule, operating window, activation, lease/ownership, admission, cancellation, retry and terminal truth. Source sampling/capture timing is an executor function governed by an approved acquisition plan, not a second scheduler or a newly authored Canvas loop.
>
> A continuous session emits finite batches. Downstream readiness is attached to the completed batch or required complete source window, not to termination of the continuous session. Partial data cannot be released by overloading a session status. Completion notifications are durable and replayable.

**Scheduling/dependency changes:** reuse the current occurrence identity and DST/window grammar; record resolved configuration revisions; distinguish batch identity, session identity and schedule occurrence; reuse current-cycle/stale-reuse policies for multi-source dependencies; preserve cancellation request versus completion acknowledgement.

**Authoring changes:** the schema table bar and safe SQL path consume stable dataset/field identities and typed source-shaped projections. A label rename must not retarget SQL/Canvas. Compile-time diagnostics name missing/incompatible fields and storage reader availability. No intelligence/BI read bypasses canonical projection.

**Analytical consequences:** configuration revisions, capture strategy, irregular spacing, gaps, quality and precision become available provenance. Existing aggregation/interpolation/eligibility authorities decide lawful use; do not create an OPC-specific average, time alignment or counter-delta kernel.

**Acceptance:** finite imports and a continuing OPC session participate in a single dependency graph; no private scheduler; no Job per sample; completed batches run exact-version transformations; wrong/incomplete/stale input dependencies refuse visibly.

### 17.10 Chapter 5 — tutorial changes

Do not add an unrelated ninth journey. Extend the existing tutorials with source-family variants:

| Existing tutorial | Proposed practical additions |
|---|---|
| T1: connect | Database vs files vs OPC profile; collector/trust proof; distinguish valid configuration from actual browse/read/subscription. |
| T2: choose/import | Select fields; import a DataBlock layout; assign technical/display names; choose record shape and Time/Change/Trigger; inspect capacity and initial history. |
| T3: prepare/map | Use logical Dump Store datasets, field identities and schema revisions; map source names to canonical targets; no direct raw BI bypass. |
| T4: load prepared data | Bind completed source batches to projection; inspect quarantine; see a continuous acquisition session remain running. |
| T7: Jobs | Use the one Job surface, understand pause vs drain/cancel, scheduled vs continuous behavior, replay and wrong-source/expired-position errors. |
| Troubleshooting/glossary | Explain unknown type, unsupported DB access, gap, stale evidence, schema drift, source log expiry, buffer capacity and retention-blocked cleanup. |

Every added step must say where to click, what happens and what proves success. A successful screen label is not proof of actual source behavior. Raw field configuration requires a trustworthy source layout or an authorized engineer's input; “no product programming required” must not be misrepresented as “no source engineering knowledge needed.”

### 17.11 Chapter 6 — deployment, capacity and lifecycle

**Extend §6.1.1.4:** document OPC server/gateway/native-driver/PPIQ-client/decoder boundaries, read-only responsibilities and actual deployment topology. State optional native drivers separately from the OPC-UA-first baseline.

**Extend sizing and C1-C4:** include fields, source scan rates, records/events, bytes, burst widths, journal durability, source log retention, projection lag, backfill coexistence and archive drain. Existing rows/s reference constants remain reference assumptions until measured; they do not imply a supported 1 ms scalar or structured-message rate.

**Proposed operational clause:**

> Each activated dataset has an approved source-impact, throughput, storage and recovery envelope. Combined source/collector/site/tenant demand is admitted through the existing capacity authority. Retention and archive policies preserve required lineage, replay receipts and consumer dependencies. No finite deployment promises indefinite lossless acquisition through arbitrary outages.
>
> Collector journals, original-source artifacts and authoritative segment payloads, where enabled, are included in backup/restore and upgrade tests. A database-only restore cannot be certified if its required payload artifacts are absent.

Add explicit overload actions and alarm thresholds based on measured demand/reserve, not fixed invented universal percentages. Add current/maximum recovery horizon, catch-up qualification, journal corruption/restart, source-disconnection and failover tests. Keep monitoring identity and severity under T-125/T-163 authority.

**Security note:** zones/conduits, trust and source-approved privileges are required design considerations. Formal security certification requires a separate scoped assessment; this chapter/report must not announce one based on architecture prose.

---

<a id="part-iii"></a>
# Part III — Recommendations for the backlog

<a id="existing-backlog"></a>
## 18. Existing-task dispositions

### 18.1 Rules for applying the recommendations

The baseline is the reviewed **v2.23.0 Worker Updated, 15 September 2026** workbook. Status below means **recorded in that workbook**, not verified current HEAD. The available extracted Backlog tab is bounded; it is not a complete census of every live backlog row. Relevant later T-265/T-266 records were read separately. [P07]

Do not allocate new task IDs until the current canonical workbook is reconciled for overlapping scopes. Use the semantic proposal names in Section 19 during decision-making. Do not increment the backlog version merely for evidence/status/dependency wording corrections. An accepted new task or material scope expansion is an authority change and warrants the appropriate new scope version under the owner's governance rule. Preserve historical estimates; re-estimate newly accepted work rather than presenting original hours as adequate.

A closed task remains closed unless there is concrete regression against its accepted scope. New source fields/configuration tables become consumers/extensions of closed foundations, not grounds to reimplement them.

### 18.2 Acquisition, frontend and data-path owners

| Existing task | Recorded state / owner | Recommended disposition | Acceptance/dependency change |
|---|---|---|---|
| **T-207 — capability truth** | Done / Worker 1 | Preserve original closure. | New capability entries are earned by executing their owning implementation tests. No blanket `supportsOPC` shortcut. |
| **T-224 — OPC session/security** | Not Started / Worker 1 | Retain core ownership. Expand only specific missing security/binding evidence. | Trusted/rejected sessions, no write/control path, collector-only source credentials, application identity and negotiated policy evidence. Keep T-207 foundation dependency. |
| **T-225 — browse/register/read/subscriptions** | Not Started / Worker 1 | Own protocol mechanics, not the complete recording-policy business layer. | Include queue/revised settings, status/filter/monitoring behavior, stable namespace/identifier binding and typed/ByteString access. SetTriggering is capability-specific and not an atomicity claim. Consume T-224. |
| **T-226 — quality/recovery/spool-to-canonical** | Not Started / Worker 1 | Retain end-to-end OPC delivery integration through Dump Store. | Consume approved field/capture/Dump Store contracts; prove journal/core ACK loss/replay/gaps; preserve Source Time Authority; do not bypass staging to write canonical directly. Completion may need accepted new producer dependencies. |
| **T-233 — Source Time persistence** | Not Started / Worker 1 | Reuse existing T-216 contract; add only explicit acquisition-provenance integration needs. | Source/server/capture/ingest roles and precision handled without a second time kernel. Don't assume capture time equals source event time. |
| **T-254 — customer-facing connector truth** | In Progress / Worker 2 | Preserve the frozen original UI/website scope; close only named provider-authority delta and approved new consumers. | UI renders operation/source/commissioning truth consistently. Protocol implementation stays elsewhere. |
| **T-265 — general UI design depth** | Not Started / Worker 2, design delivery | Extend non-OPC configuration journey, field/retention/capacity/Job design handoffs. | Design acceptance is not backend/frontend runtime completion. Reuse B1-B5 components and roles. |
| **T-266 — OPC UI workflows** | Not Started / Worker 2, design delivery | Add raw-layout naming, three recording modes, capture strategy and requested/revised/capacity/gap states. | Own interaction specifications/illustrations only. A separate implementation owner must wire actual controls/services. Worker 1 reviews source semantics. |
| **T-101 — quarantine/Mapping Health** | Not Started / Worker 2 | Consume new parser/layout/schema/source-gap evidence through existing status surfaces. | Do not label intentional deadband filtering as quarantine. Permission-safe raw inspection/reprocessing uses exact revisions. |
| **T-104 — projection provenance/idempotence** | Not Started / Worker 1 | Extend provenance references only where required by the accepted acquisition contract. | Exact batch/schema/acquisition/field/transform identity; source retry differs from new revision; preserve existing canonical supersession rules. |
| **T-261 — Transformation/Projection executor** | In Progress / Worker 2 | Preserve existing executor work; consume stable dataset reader and batch/window contracts. | No private ingestion scheduler, no latest-data shortcut, no reimplementation of field or Job authorities. New acquisition integration must not retroactively erase existing acceptance. |
| **T-262 — canonical field-binding contract** | In Progress / Worker 2; committed producer evidence recorded | Preserve producer contract; reconcile existing integration boundary. | Source FieldId/layout references feed the existing governed canonical binding; no second canonical target registry. |
| **T-213 — customer data intake** | Done / Worker 2 | Preserve closure; add acquisition commissioning questions as a consumer/extension. | Capture source versions, layouts, buffering/ACK semantics, source time, history, retention and budget unknowns; the example CSV remains illustrative. |

### 18.3 Jobs, cursor and foundation owners

| Existing task | Recorded state / owner | Recommended disposition | Acceptance/dependency change |
|---|---|---|---|
| **T-106 — canonical Jobs** | In Progress / Worker 3 | Explicit accepted extension for continuous executor lifecycle/session/batch distinctions. | Preserve dataset target exception, one schedule grammar, occurrence/lease/cancel/terminal truth. Test with registered executors. **Do not depend on T-261 or OPC feature completion to prove orchestration.** |
| **T-107 — bounded admission** | Not Started / Worker 3 | Extend current resource model for persistent acquisition occupancy and controlled backfill coexistence. | No second admission service. Per-source and aggregate ceilings; bounded queues; no sample-to-Job explosion. Existing dependencies require reconciliation rather than blind replacement. |
| **T-108 — delta/cursor/load/replay** | Not Started / Worker 1 | Retain current source-position/receipt authority and approved window/source budget laws. | Add qualified provider position kinds and receipt integration as explicit slices; current tuple semantics remain. Do not make it depend cyclically on a downstream reader/executor extension. |
| **T-110 — large-data mechanics** | M3 / Worker 1 | Keep advanced chunking/parallelism scope. | M2 correctness and bounded operation cannot be deferred to this M3 task. High-volume optimized payload placement may consume its measured mechanics. |
| **T-087/T-088/T-089/T-090/T-091** | Foundation closures recorded | Consume canonical migration/lifecycle/impact authorities; preserve accepted scope. | New acquisition kinds/schema detail require owned extensions and migrations, not rewriting those producers or inventing a new lifecycle. |
| **T-251 — physical catalogue** | Done / Worker 1 | Preserve closure and consume catalogue enforcement. | Every new table/view/column has semantic name, owner, lifecycle, source/target schema, retention and RLS; no task-ID or generation-suffix object names. |
| **T-252/T-260 — disposable certification/builders** | Done / Worker 1 | Preserve and reuse. | Tests provision isolated approved fixtures; no shared `ppiq_app` or retained presentation mutation as certification. |

### 18.4 Security, operations, lifecycle and release owners

| Existing task | Recorded state / owner | Recommended disposition | Acceptance/dependency change |
|---|---|---|---|
| **T-112/T-114 — tenancy/keys/RLS** | Not Started / Worker 1 | Extend enforcement to acquisition definitions, batches, receipts, dataset readers and artifacts. | Two-tenant negative tests, owner/BYPASSRLS posture, enforceable tenant-aware FKs/uniqueness. |
| **T-113 — secrets/configuration** | Not Started / Worker 1 | Collector-local source secrets, trust references, safe rotation and signed configuration. | No plaintext core OT credentials, arbitrary endpoint/file access or executable configuration. |
| **T-119 — roles** | Not Started / Worker 1 | Use current role catalogue for configure, preview, publish, activate, pause, retention and archive access. | Permission denied at backend as well as UI; raw payload errors do not leak restricted values. |
| **T-123 — install/upgrade/rollback** | Not Started / Worker 1 | Add collector, adapter/decoder and journal compatibility to deployment evidence. | Upgrade rollback preserves positions, receipts, layouts and safe ownership. |
| **T-124 — backup/restore** | Not Started / Worker 1 | Include every authoritative acquisition artifact enabled by the approved storage profile. | Restore references and payload consistently; no database-only green result when artifacts are required. |
| **T-125 — minimum health/metrics** | Not Started / Worker 1 | Add acquisition, durable-position, source-gap, capacity and projection-backlog metrics through the current authority. | No second feature health registry; no log per sample. Minimum safe operation is M2. |
| **T-154/T-155** | M3 / Workers 1/3 | Advanced physical query/index/partition and calibrated capacity tuning. | A proposed generic store is not assumed adequate until the relevant workload is measured. |
| **T-159 — C1-C4 certification** | M3 / Worker 3 | Extend current envelopes to mixed sources, variable payloads and recovery workloads. | Record hardware, versions, dataset shape, rates, retained volume and concurrent load. Do not relabel rows/s as values/s. |
| **T-163 — full observability/SLOs** | M3 / Worker 1 | Full operational product deepening, not a prerequisite for reporting M2 data loss honestly. | Long-term alerting/SLO/support paths consume the same minimum metrics. |
| **T-198/T-199 — logs and log retention** | M3 / Worker 2 | Preserve log-specific ownership. | **Do not silently assign source dataset payload retention to T-199.** Reuse common safe archive primitives only through a documented interface. |
| **T-200 — system defaults/localization** | M3 / Worker 2 | Global defaults and language integration, not sole owner of M2 source configuration. | Field labels/localization and inheritance consume this authority; M2 capacity/retention cannot wait silently for all M3 settings. |
| **T-247 — Day-1 golden journey** | Not Started / Worker 2 | Extend integrated proof to named source families and three capture modes with field naming. | Same binary, actual source/Jobs/Dump Store/Canvas path; input fixtures and prototypes distinctly labelled. |
| **T-150 — M2 release gate** | Not Started / Worker 1 | Include every accepted M2 acquisition producer/consumer and minimum safety obligation. | Read the actual current Release-1-MUST set; do not rely on an obsolete hardcoded predecessor list. |
| **T-153 — real-source certification** | M3 / Worker 1 | Retain broader source/site/endurance qualification. | A real M2 customer source still requires its necessary commissioning before use; T-153 is not permission to call uncommissioned M2 acquisition production-ready. |

### 18.5 No duplicate work and no artificial green claims

The backlog should explicitly distinguish **new functionality**, **integration of a reusable producer**, **compatibility correction**, **design/UI specification**, and **actual runtime acceptance**. In particular:

- Field-layout decoding is not already complete because `source_field_definitions` exists.
- Periodic/group capture is not complete because a sampling parameter exists.
- Dataset retention is not complete because log retention is planned.
- Native PLC access is not complete because an OPC client exists.
- A Dump Store reader is not complete because a JSON preview can render rows.
- A same-binary golden journey cannot be closed with static mockups or source-label checks.

---

<a id="new-backlog"></a>
## 19. Candidate new work packages

These are **semantic proposal identifiers, not allocated backlog task IDs**. A final current-workbook overlap check must decide whether each is a genuinely new task or an explicit acceptance slice under a suitable existing open owner. No hours are assigned without source-level preflight and the accepted storage/source matrix.

### 19.1 ACQ-CONFIG — acquisition configuration and stable field authority

**Recommended owner:** Worker 1; Worker 2 consumes its API/validation contract.  
**Release disposition:** candidate M2 requirement for the approved source set.

**Description:** extend the current dataset/field/lifecycle authority with provider-specific source selectors, stable field identity, layout/schema revisions, capture groups, recording policies and exact-version activation bindings. Register any new definition kinds explicitly; keep runtime negotiated evidence separate.

**Acceptance:** all three recording policies and raw/typed field definitions save/reopen/version correctly; display rename preserves identities; address/type edits fork a reviewed revision; invalid combinations fail server-side; old stored definitions remain readable; permissions and cross-tenant references are enforced; config export/import preserves meaning without exporting source secrets.

**Consumes:** existing migration, lifecycle, capability and role authorities.  
**Excludes:** protocol client implementation, durable ingestion, new scheduler, canonical semantic mapping, UI implementation.

### 19.2 ACQ-LAYOUT — bounded DataBlock/message decoding and buffered-record identity

**Recommended owner:** Worker 1.  
**Release disposition:** candidate M2 for the explicitly supported raw/structured input profiles; unsupported vendor codecs remain unavailable.

**Description:** implement the approved declarative field layout, absolute/relative addressing, byte/bit/array/string rules, typed output and source publication/sequence validation. Handle buffered records as unique source events without process write-back.

**Acceptance:** V05-V09 and V22-V28; known-answer fixtures from at least two non-identical source layouts; no customer-specific handler code for supported grammar; invalid bounds and decoder resource budgets enforced; original layout references survive replay; unknown ACK/direction enums refuse commissioning rather than being guessed.

**Consumes:** ACQ-CONFIG and supported adapter raw/typed payload contract.  
**Excludes:** a native S7/Modbus driver unless separately approved; arbitrary user plugins; business conversion/relationship publication.

### 19.3 ACQ-POLICY — recording and trigger/capture executor

**Recommended owner:** Worker 1; Worker 3 owns the generic Job/admission integration contract.  
**Release disposition:** candidate M2 core behavior within a qualified rate/source envelope.

**Description:** execute PERIODIC, ON_CHANGE and TRIGGERED policies with deterministic state, persisted baseline, exact fields/layout, capture strategy, quality/time handling, source gap truth and bounded output assembly. Reuse protocol operations from T-225 and time authority from T-216/T-233.

**Acceptance:** V10-V30; constant values under periodic mode; tiny representable changes; strict/cumulative band; quality transitions; startup/edge/re-arm/reset; incomplete/gapped snapshots; semantic filter pushdown equivalence; no latest-cache substitution for source-latched capture; no private scheduling or one-Job-per-sample behavior.

**Consumes:** ACQ-CONFIG, ACQ-LAYOUT where needed, T-225 operation contract, durable journal/record contract and T-106/T-107 executor interfaces.  
**Excludes:** another OPC session implementation, another Source Time kernel or another long-term storage engine.

### 19.4 ACQ-PROVIDERS — qualified relational and file acquisition convergence

**Recommended owner:** Worker 1; frontend consumer Worker 2.  
**Release disposition:** all user-required provider families are target scope; exact operations/versions must be release-qualified. Do not defer a required family silently. Advanced CDC variants beyond approved initial-read/incremental contracts may be separately phased.

**Description:** complete provider-specific selection/type/consistency/revision/parse behavior behind the shared dataset contract. Reuse existing readers, budgets and T-108 cursor/receipt authority. Implement only advertised capabilities for SQL Server, Oracle, PostgreSQL, MySQL, Excel and CSV.

**Acceptance:** V01, V31-V40, V49 and mixed-source retry; equal-watermark paging; source updates/deletes; no-change rerun; source consistency under mutation; source position expiry; workbook sheet/range; quoted multiline CSV; replaced/renamed/append files; exact parser identity; bounded source load. Test each advertised engine/version/path, not one mock labelled with several provider names.

**Consumes:** ACQ-CONFIG, shared Dump Store writer/reader, T-108 and Job interfaces.  
**Excludes:** source DBA reconfiguration, automatic enablement of log capture, independent cursor persistence, full native replication into canonical tables.

### 19.5 ACQ-DUMP — dataset-oriented Dump Store and consumer contract

**Recommended owner:** Worker 1; Worker 2 integrates authoring consumers on a separate owned slice.  
**Release disposition:** candidate M2 baseline; optional high-speed artifact/typed profiles require explicit additional qualification.

**Description:** resolve logical versus physical staging semantics; implement exact batch/session/window/schema/source-revision envelope and one authoritative payload placement; expose typed tenant-scoped datasets to preview, Canvas and safe SQL; extend existing receipt/batch mechanics without a second cursor authority.

**Acceptance:** V34-V35, V41-V48, V52 and V60; lost ACK/retry, integrity conflict, precision preservation, partition-safe identity, completed-batch visibility, original-bytes distinction, source-shaped reader equivalence and exact-version projection input. No independent duplicate JSON/typed payload authorities.

**Consumes:** current staging/migration/lifecycle/tenant authorities and the accepted T-108 receipt contract.  
**Excludes:** reimplementation of T-108 source cursors or T-261 transformation mathematics; mandatory distributed/columnar storage for all customers without evidence.

### 19.6 ACQ-LIFECYCLE — source dataset capacity and retention control

**Recommended owner:** Worker 1; Worker 3 reviews admission/calibration; Worker 2 owns UI integration through ACQ-UI.  
**Release disposition:** minimum bounded ingestion, reserve awareness and safe retention are candidate M2 requirements. Advanced storage tier automation may be M3, but absence must limit activation honestly.

**Description:** bind per-dataset retention and replay/dependency floors to aggregate source/collector/site/tenant capacity; expose source-data dry-run/archive/reclamation state; integrate existing metrics and backup/restore. Distinguish dataset lifecycle from T-199 log retention.

**Acceptance:** V50-V56 and V60; combined-source capacity rejection, future growth basis, finite spool/outage horizon, drain greater than live input, unprocessed-data deletion refusal, verified archive-before-delete, manifest/receipt survival, restore consistency and protected source change-log retention.

**Consumes:** ACQ-DUMP, current budget/admission contracts, T-124/T-125 integration and catalogue classification.  
**Excludes:** duplicate global scheduler, arbitrary global deletion, invented compression ratios, formal legal/security certification.

### 19.7 ACQ-UI — executable unified interface configuration

**Recommended owner:** Worker 2.  
**Release disposition:** candidate M2, consuming per-surface approved handoffs rather than waiting to design until every backend component is complete.

**Description:** implement the approved B1-B5 source configuration experience for all named source families, field-layout naming, group recording policies, storage/capacity and Job binding. Consume T-265/T-266 design, real API validation and existing controls/roles.

**Acceptance:** keyboard/RTL, bulk editing, exact errors, save/reopen/revision behavior, source preview, capability truth, three modes, field picker identity, capacity refusal and Job controls; actual browser/service integration, not only prototype frames. Closing the browser does not stop the collector.

**Consumes:** ACQ-CONFIG plus the corresponding executable layout/policy/provider/Dump Store/lifecycle and Job contracts for each advertised surface.  
**Excludes:** UI-local acquisition loops, hardcoded provider capability truth, new page/definition/field authority or direct browser-to-PLC calls.

### 19.8 Optional proposal — native PLC transport adapters

Native S7, Modbus or other PLC transports are **not automatically included** by the OPC-UA-first decision. When approved, create a separately owned semantic task with a source/version/security matrix, driver licensing/dependency review, safe read budget, raw type/layout contract, fault tests and real-source qualification. It must emit the same accepted acquisition records and reuse the same policies/Jobs/Dump Store.

This is a product scope decision, not a workaround to force every unsupported DB address through an OPC client.

---

<a id="delivery"></a>
## 20. Dependencies, release boundaries and adoption sequence

### 20.1 Contract handoffs versus completion gates

The intended handoffs are:

```text
Accepted chapter decisions + frozen reusable foundations
       -> ACQ-CONFIG contract
            -> ACQ-LAYOUT contract
            -> ACQ-POLICY contract
            -> ACQ-PROVIDERS contract
            -> ACQ-DUMP contract
            -> ACQ-LIFECYCLE contract

T-224 -> T-225 protocol operations
T-216/T-233 -> time authority
T-106 -> Job executor/lifecycle/dependency contract
T-107 -> admission contract
T-108 -> source position/receipt correctness contract
T-265/T-266 -> approved per-surface design handoffs

Implemented producers + executed integration
       -> ACQ-UI actual workflow
       -> T-226 OPC end-to-end proof / T-247 mixed-source golden journey
       -> T-150 applicable M2 release gate
       -> T-153/T-159 broader site/endurance/capacity qualification
```

This is a **contract handoff map**, not an automatically applied task dependency list. Several producers can be developed against frozen interfaces with test doubles, but their final integration acceptance needs real implementations.

A specific cycle to avoid: making an extended T-261 wait for ACQ-DUMP, making ACQ-DUMP wait for full T-108, while current T-108 already waits through T-104 for T-261. Instead preserve existing producer closure boundaries, define a backward-compatible dataset-read/receipt seam, and allocate the new integration acceptance after the appropriate producers. Do not “fix” the cycle by dropping required correctness tests.

T-106 likewise remains independently testable through registered executors; do not make generic orchestration completion depend on every future provider.

### 20.2 Release boundary recommendation

**Recommended M2 minimum:** configuration-driven source selection and field naming; approved basic source operations for the committed provider matrix; all three recording modes where source semantics permit; safe decoder subset; read-only acquisition; finite durable batches; idempotent replay; exact-version Job binding; bounded capacity/retention; usable source-shaped preparation and actual same-binary workflow proof.

**Not implied by M2:** arbitrary native PLC transports, every vendor/version, hard-real-time 1 ms across 1,000 fields, unlimited outage recovery, globally atomic multi-PLC captures, every CDC mode, automatic cold-tier processing or broad HA certification.

**M3 candidates:** measured high-volume payload alternatives, broader source/version/endurance matrix, source-local high-speed profiles, advanced archive retrieval/HA, calibration and full SLO tooling. A feature needed for the first customer's accepted M2 workload cannot be deferred merely because it is difficult; either include/qualify it or negotiate an explicit accepted boundary.

### 20.3 Adoption sequence

1. **Approve the key decisions** in Appendix C, including physical Dump Store semantics, configuration lifecycle integration and allowed capture consistency claims.
2. **Edit the owning chapters** with exact clauses and their acceptance; reconcile the current Chapter 4 body and derived illustrations.
3. **Reconcile the single backlog** for overlap, assign any genuinely new IDs, update precise descriptions/acceptance/handoffs and re-estimate changed scope. Preserve closure evidence and historical figures.
4. **Freeze reusable contracts** for field identity, capture records, source positions, dataset readers, Jobs and receipts before parallel implementation.
5. **Implement vertical slices**, not disconnected screens: one relational source and one named raw/typed PLC dataset each traverse configuration -> Job -> durable Dump Store -> governed projection.
6. **Add remaining advertised providers and modes** with the same invariants and provider-specific tests.
7. **Commission and qualify the release envelope** using actual sources where required, mixed loads and failure/recovery cases. Update capability truth only when its exact proof exists.

No original design or workbook has been edited by this report. The sequence describes recommended adoption, not background work scheduled by the reviewer.

### 20.4 Definition of done for the accepted change set

The change set is complete only when each accepted requirement has an owning chapter clause, an allocated implementation owner, a real runtime path, passing targeted and integration acceptance, compatible historical behavior, operational visibility and an honest supported envelope. A configuration-only demonstration, a drawn diagram, a green SDK example or a raw JSON preview cannot close the whole workflow.

---
<a id="appendix-a"></a>
# Appendix A — Illustrative configuration

This is a **conceptual contract example**, not an API-ready payload for the current build. IDs are symbolic references, not actual records. The example assumes an explicitly supported raw/ByteString block transport and an approved big-endian REAL32 layout; it does not infer that configuration for the owner's sample file.

```json
{
  "datasetRef": "configured-process-dataset",
  "sourceBindingRef": "approved-source-binding",
  "schemaRevisionRef": "published-layout-revision",
  "layout": {
    "addressSpace": "DeclaredDataBlockBytes",
    "dataBlockNumber": 20,
    "readStartByte": 0,
    "readLengthBytes": 21,
    "byteOrder": "BigEndian",
    "fields": [
      {
        "fieldId": "field-temperature",
        "key": "Temperature",
        "displayName": "Process temperature",
        "byteOffset": 4,
        "type": "REAL32",
        "sourceUnit": "degC"
      },
      {
        "fieldId": "field-speed",
        "key": "Speed",
        "displayName": "Process speed",
        "byteOffset": 8,
        "type": "REAL32"
      },
      {
        "fieldId": "field-pressure",
        "key": "Pressure",
        "displayName": "Process pressure",
        "byteOffset": 12,
        "type": "REAL32"
      },
      {
        "fieldId": "field-counter",
        "key": "SaveCounter",
        "displayName": "Capture counter",
        "byteOffset": 16,
        "type": "UINT32"
      }
    ]
  },
  "recordingGroups": [
    {
      "groupId": "periodic-speed",
      "mode": "PERIODIC",
      "fieldIds": ["field-speed"],
      "periodMs": 250,
      "captureMethod": "READ_AFTER_DUE_TICK",
      "freshnessPolicyRef": "approved-read-validity-policy",
      "missedTickPolicy": "RECORD_GAP_NOT_SYNTHETIC_HISTORY"
    },
    {
      "groupId": "changed-temperature",
      "mode": "ON_CHANGE",
      "fieldIds": ["field-temperature"],
      "requestedMonitoringMs": 100,
      "deadband": {
        "kind": "ABSOLUTE",
        "value": "0.1",
        "unit": "degC",
        "comparison": "STRICT_GREATER_THAN",
        "baseline": "LAST_DURABLY_RECORDED_VALUE"
      },
      "output": "CHANGED_FIELDS_ONLY"
    },
    {
      "groupId": "counter-associated-capture",
      "mode": "TRIGGERED",
      "triggerFieldId": "field-counter",
      "condition": "COUNTER_INCREMENT",
      "requestedMonitoringMs": 100,
      "startupPolicy": "BASELINE_WITHOUT_EVENT",
      "counterContractRef": "approved-source-epoch-reset-policy",
      "fieldIds": ["field-temperature", "field-speed", "field-pressure"],
      "captureMethod": "READ_AFTER_TRIGGER",
      "consistencyClaim": "NOT_SOURCE_ATOMIC",
      "incompleteCapturePolicy": "MARK_INCOMPLETE"
    }
  ],
  "sourceTimeAuthorityRef": "approved-source-time-authority",
  "capacityProfileRef": "qualified-workload-profile",
  "retentionPolicyRef": "approved-dataset-retention",
  "jobBinding": {
    "jobClass": "import",
    "sourceDatasetRef": "configured-process-dataset",
    "executionMode": "CONTINUOUS",
    "acquisitionRevisionPolicy": "PINNED"
  }
}
```

Important consequences:

- The same physical field can feed a periodic, on-change or trigger-associated output without acquiring a new identity for every use.
- Group IDs distinguish the logical output streams; receipt/capture identity includes the relevant stream/policy revision where appropriate.
- Requested settings remain configuration. Resolved NodeIds, negotiated intervals and measured rates do not mutate this published payload.
- Unit declarations for speed/pressure are intentionally not invented. They must be established before unit-dependent calculations or bands are used.
- A read-after-trigger example does not claim a source-atomic snapshot. Selecting an atomic source-event profile would require a different proven source contract.
- Field layout and groups reference the same field identities used by configuration, Jobs, Dump Store and later mapping.

---

<a id="appendix-b"></a>
# Appendix B — Example-file calculations, not production sizing

## B.1 File identity and complete CSV parsing

**File:** `PLCMessages-1786790425225.csv`  
**SHA-256:** `8a90f0f0f77802a423577b6212aefedbe4e2baba89867cc0659c5b4ae5fbc9a9`

| Measured file property | Value |
|---|---:|
| Logical CSV records | 633 |
| Columns | 23 |
| Records with `Enable=1` | 603 |
| Records with `Enable=0` | 30 |
| Enabled records with positive `CyclicTime` | 317 |
| Enabled records with zero `CyclicTime` | 286 |
| Distinct `PlcConnectionId` values | 24 |
| Maximum `DataByteLength` | 30,508 bytes |

These are properties of the supplied **example catalogue**, not a count of live signals or actual PLCs. Physical line count is not CSV record count because some quoted descriptions span lines. `Type`, `Direction`, `ConverterType`, `AckRequired`, `BufferBlocksMaxNo` and `CyclicTime` require source-system definitions before runtime meaning is assigned. [P08]

## B.2 Conditional full-payload calculation

Only under both assumptions—`CyclicTime` is milliseconds and the entire `DataByteLength` payload is stored once each positive enabled cycle—the equivalent payload rate is:

```text
sum(DataByteLength * 1000 / CyclicTime)
  = 397,115.3126293996 bytes/second
  = 34.31076301118012 decimal GB/day
  = approximately 1.0293 decimal TB over 30 days
```

The calculation excludes zero-cycle entries and other database/file sources. It does not include storage overhead, compression, protocol framing, actual event frequency or incremental extraction from buffers. It is **not a production estimate**. Its design value is to expose the mistake of saving every reread block as new information.

No commissioning, storage procurement or M2 release rate is chosen from these numbers.

## B.3 Executed reference checks

All listed checks passed in the local reference models described in Section 15.3. They are not PPIQ product tests.

| # | Reference check | Scope/result |
|---|---|---|
| 1 | `capacity_1ms` | PASS — 1000 fields, periodic 1 ms: 86,400,000,000 values/day |
| 2 | `capacity_100ms` | PASS — 1000 fields, periodic 100 ms: 864,000,000 values/day |
| 3 | `capacity_250ms` | PASS — 1000 fields, periodic 250 ms: 345,600,000 values/day |
| 4 | `capacity_500ms` | PASS — 1000 fields, periodic 500 ms: 172,800,000 values/day |
| 5 | `capacity_1000ms` | PASS — 1000 fields, periodic 1000 ms: 86,400,000 values/day |
| 6 | `deadband_cumulative` | PASS — Reference comparison is last accepted value, not last observed value. |
| 7 | `deadband_strict_boundary` | PASS — Strict greater-than threshold; decimal fixture avoids binary boundary ambiguity. |
| 8 | `zero_deadband_distinct` | PASS — No hidden tolerance applied to distinguishable fixture values. |
| 9 | `periodic_constant` | PASS — Ten logical ticks in [0,1000) ms even for constant values; not a measured scheduler. |
| 10 | `rising_edge_once` | PASS — Sustained true is not a repeated edge. |
| 11 | `startup_true_no_event` | PASS — Default startup establishes baseline without invented edge. |
| 12 | `short_pulse_unobservable` | PASS — Two different physical histories give identical samples at 0/100 ms; an intervening pulse cannot be inferred. |
| 13 | `counter_gap` | PASS — Counter jump identifies two unobserved intervening increments; no payload reconstruction. |
| 14 | `declared_wrap` | PASS — Unsigned-16 declared modulo gives delta 2 only when wrap/epoch assumptions are established. |
| 15 | `counter_reset_not_wrap` | PASS — Distinct reset/wrap histories yield the same endpoint readings; source epoch/policy is necessary. |
| 16 | `layout_bounds` | PASS — All example fields fit within a 21-byte block; BOOL occupies a declared bit in its carrier byte. |
| 17 | `layout_disjoint` | PASS — No undeclared byte overlap in the illustrative layout. |
| 18 | `payload_relative_offset` | PASS — Source absolute byte 108 within read starting at 100 maps to offset 8. |
| 19 | `real32_roundtrip` | PASS — Example big-endian REAL32 decode; byte order is a fixture choice, not inferred source metadata. |
| 20 | `float32_resolution` | PASS — Spacing at Float32 value 850 is 0.00006103515625. |
| 21 | `float32_small_change_lost` | PASS — Widening to Float64 after source rounding cannot recover the lost distinction. |
| 22 | `composite_cursor_pages` | PASS — Composite cursor covers 13 records, including 11 equal-watermark records, over five pages. |
| 23 | `scalar_cursor_counterexample` | PASS — Watermark-only continuation omits unseen records tied at watermark 10. |
| 24 | `first_receipt` | PASS — First source revision accepted. |
| 25 | `retry_receipt` | PASS — Retry of the same identity/revision deduplicated. |
| 26 | `legitimate_update` | PASS — Same business key, new source version is preserved. |
| 27 | `tenant_isolation_key` | PASS — Receipt identity is tenant scoped. |
| 28 | `same_values_new_event` | PASS — Equal payloads do not collapse distinct event sequences. |
| 29 | `restart_epoch` | PASS — Restarted sequence identities require source epoch context. |
| 30 | `atomic_failure_model` | PASS — Reference atomic unit leaves no advanced cursor after failure; not a database test. |
| 31 | `atomic_commit_model` | PASS — Rows and cursor become visible together in reference model. |
| 32 | `spool_capacity` | PASS — Example 250 kB/s for one hour needs 900 MB before overhead and reserve. |
| 33 | `drain_capacity` | PASS — At 750 kB/s drain and 250 kB/s live input, example catch-up takes 1800 seconds. |
| 34 | `insufficient_drain` | PASS — Drain equal to live input never reduces backlog. |
| 35 | `csv_multiline_parse` | PASS — Logical CSV records counted with quoted multiline fields, not physical line count. |
| 36 | `csv_enabled_split` | PASS — Illustration only: 603 enabled, 317 positive-cycle records, 286 zero-cycle records. |
| 37 | `csv_conditional_rate` | PASS — Assumed cycle unit ms and full-payload-per-cycle; not actual throughput. |

**Executed result:** 37/37 reference checks. No product runtime or timing certification is implied.

---

<a id="appendix-c"></a>
# Appendix C — Decisions requiring explicit approval

| Decision | Recommended ruling | What approval does not mean |
|---|---|---|
| Product topology | OPC-UA-first; qualified external/embedded server and gateway drivers, PPIQ client/decoder at the collector | Does not authorize native S7/Modbus or PLC control automatically. |
| Main recording grammar | PERIODIC / ON_CHANGE / TRIGGERED, with explicit modifiers | Does not promise every rate or capture strategy on every source. |
| Field configuration | Stable IDs, user naming and versioned raw/typed layouts inside the same dataset authority | Does not make unexposed source addresses reachable. |
| Recording baseline | Explicit source-notification semantics or last-durable-PPIQ-record semantics; no hidden combination | Does not establish arbitrary filter-pushdown equivalence. |
| Source event consistency | Only claim source-atomic/event-time capture with source contract evidence | Close timestamps and one destination transaction are insufficient. |
| Job model | One Job authority, finite and continuous executors, finite completed batches | No private OPC scheduler or sample-level Job rows. |
| Dump Store | Stable logical datasets, one authoritative payload placement, bounded baseline storage | Does not certify a giant JSONB store for every high-speed workload. |
| Original artifacts | Explicit policy-bound original bytes where required, with manifests/backup/access | Does not replace all Dump Store rows with object storage silently. |
| Dataset lifecycle | Source payload retention distinct from log retention, with dependency/replay floors | Does not authorize immediate deletion after projection. |
| Capacity | Source/collector/site/tenant aggregate admission and measured workload profiles | A configurable 1 ms field is not a 1 ms performance promise. |
| Backlog | Reuse closed foundations; formally expand open owners and allocate genuinely new scopes | This report does not allocate IDs, change task statuses or assert original estimates remain adequate. |
| Document governance | Integrate accepted clauses into the six chapters and backlog; archive this rationale | This report never becomes a seventh design authority. |

**Overall recommendation:** adopt the owner's simple configuration model, but implement it over explicit identities, source contracts, durable boundaries and finite budgets. The enterprise quality of the solution comes from preserving these meanings through failure, replay, change and scale—not from exposing more input boxes or collecting every address at the fastest rate.

---

<a id="references"></a>
# Sources and traceability

## Internal and owner sources

Internal references identify the exact reviewed file/version or source passage. The source list is a retrieval/coverage record, not a claim that every whole file was audited or that its current runtime matches it. Rendered line locators refer to the successfully retrieved textual representation, not to a source-code file's own line numbers unless stated explicitly.

<a id="u01"></a>
### U01 — Owner requirements and rulings in this conversation

The owner specified all named provider families; configuration from the DB Link/interface experience; three recording choices; configurable DataBlock member naming; common Dump Store; Job integration; generic customer-independent behavior; finite-storage concerns; and that the PLC CSV is only an example of something that could arrive. The owner requested this advisory Markdown report and recommendations for the existing design/backlog, not modifications to those files.

<a id="p01"></a>
### P01 — Chapter 1, available v4.10 text

`PPIQ_Chapter1_Marketing_and_Sales(20260912-092922).md`, internally marked Version 4.10. Reviewed anchors: §1.0.1 dataset-neutral same-binary promise; §1.3.f source-specific evidence authority; §1.7 claim limits; §1.8 objections; §1.10 demonstration disclosure; §1.11 commercial promise traceability. The retrieved heading/selected-content lines span approximately 38-472. This is not relabelled as a separately recovered full v4.10.3 file.

<a id="p02"></a>
### P02 — Chapter 2, available v4.10 text

`PPIQ_Chapter2_Technical_Overview(10).md`, internally marked Version 4.10; 1,345 rendered lines. Reviewed anchors: source-neutral release/journey model, §2.0.3, §2.0.9 read-only collector/capability boundary (around line 109), and acquisition inclusion/exclusion statements. Historical release dates in this copy are superseded by P03/P07's later planning basis.

<a id="p03"></a>
### P03 — Chapter 3, v4.10.3 illustrated reading edition

`PPIQ_Chapter_3_Illustrated_v4.10.3.docx`; package revision 14 September 2026; 9,439 rendered text lines in successful retrievals. Relevant locators:

- Around lines 112-158: authority, package revision, current planning basis and required DF/page contract structure.
- Lines 318-632: DF1 connection/read-only proof, DF2 registration, DF3 incremental staging/cursor/receipt, and DF4-DF5 preparation/projection.
- Around lines 6276-6279: F4 Job surface binding to Chapter 4 §§5.3.2a and 5.3.6.
- Lines 7280-7444: environment roles and §4.5.3 `import_batches`, `staging_records`, cursor/drift/collector evidence.
- Lines 7490-7547: signal/aggregation semantics, observation values, source/server/ingest timestamps, raw value, and the dangling Chapter 7 retention reference.
- Lines 7590-7629: source datasets/fields and configuration/Job catalogue.
- Lines 7694-7771: §4.5.5a import dataset-binding exception, target version behavior and physical classification.
- Lines 8948-8975 and later topology portions: source -> collector -> staging -> canonical flow and no direct core OT initiation.

The read was text-focused and scoped to these contracts. The file's embedded illustrations were not comprehensively audited. Later intermittent retrieval failures do not invalidate earlier successful quoted passages or establish source absence.

<a id="p04"></a>
### P04 — Chapter 4 coverage boundary

The full current v4.10.3 Chapter 4 body was not recovered in this review. Current ownership of §5.3.2a schedule grammar, §5.3.6 dependency freshness and the canonical Job contract is supported by P03 and P07. The locally mounted `PPIQ_Chapter4_Specific_Technical_Function_Description(2).md` identifies an older v4.5 body and was used only to locate historical §5.2 authoring and §5.3 execution headings. It does not override current contract references. Proposed Chapter 4 clauses require reconciliation against the current owning file before integration.

<a id="p05"></a>
### P05 — Chapter 5, available v4.10 text

`PPIQ_Chapter5_Tutorial_User_Journey(20260912-092922).md`, internally marked Version 4.10; 1,015 rendered lines. Reviewed §6.0.0 first-week same-binary journey; eight-tutorial map; T1/T2/T3/T4/T7 and troubleshooting/glossary headings. Example anchors: tutorial T1 around line 153, T2 252, T3 337, T4 471, T7 751, troubleshooting 943. The chapter expressly derives its controls/semantics from Chapter 3 rather than owning a second journey.

<a id="p06"></a>
### P06 — Chapter 6, available v4.10 text

`PPIQ_Chapter6_Infrastructure_Website_Administration(7).md`, internally marked Version 4.10; 2,237 rendered lines. Reviewed authority/components; §6.1.1.4 connector capability/OPC boundary (around lines 153-161); §6.1.5.8 C1-C4 capacity certification (around 474-505); and related deployment/sizing/backup/operational references. The file explicitly labels capacity constants as pending measured certification; its historical dates are not current delivery commitments.

<a id="p07"></a>
### P07 — Current reviewed execution workbook

`PPIQ_Backlog_v2.23.0_15Sep2026_Worker_Updated.xlsx`, current authority tab and relevant task rows/worker/source-review records; 38 tabs and 10,555 rendered lines reported by the source reader. The authority tab identifies v4.10.3 functional design, v2.23.0 execution scope, 179 active tasks and the relative M2/M3 planning basis.

The bounded local text export contains only part of the workbook; it was used to parse the relevant task rows, not to assert a complete task census. T-265/T-266 design records were also retrieved separately. The same-version `PPIQ_Backlog_v2.23.0_15Sep2026_Source_Reviewed.xlsx` was used where already retrieved task explanations corroborate the Worker Updated scope. No full current Git/DB/browser validation is inferred from workbook status.

Important reviewed task anchors: T-106 canonical Jobs; T-107 admission; T-108 delta/cursor/receipt; T-104/T-261/T-262 projection; T-224/T-225/T-226 OPC; T-233 time; T-254 capability convergence; T-265/T-266 design; T-199 explicitly log retention; T-247/T-150 release journey/gate; T-153/T-159 certification.

<a id="p08"></a>
### P08 — Illustrative PLC message catalogue

`PLCMessages-1786790425225.csv`; the complete uploaded backing file was parsed. Its 23-column header includes message identity/name/description, type/enable, DataBlock/byte fields, cyclic time, converter type, connection/node namespace, buffer/direction/origin/machine and acknowledgement fields. It does not independently define every inner member's decoder, the source enum meanings or the actual deployed event rate. File hash and measured catalogue properties are in Appendix B.

<a id="p09"></a>
### P09 — Scoped repository export evidence

`01_Backend_Core_14Sep2026_104846.txt` and `07_Tools_Validation_Misc_14Sep2026_104846.txt`, previously retrieved passages in this conversation:

- `Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs`, exported block around line 49,614; capability registry and typed unavailable operations.
- `Backend/PlantProcess.Infrastructure/Connectors/Historian/OpcUaHistorianConnector.cs`, exported block around line 125,490; configuration validation and legacy metadata.
- `Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs`, exported block around line 138,440; existing contract-only sample envelope.
- `tools/edge-agent/ppiq-edge-agent.cjs`, exported block around line 60,188 in the tools export; deterministic sample generation and sample-package behavior.

These are source observations for named paths in the supplied snapshot, not a runtime verification or an exhaustive finding that no other implementation exists.

## Official external sources

**Access/verification date:** 16 September 2026. Version-specific links are retained intentionally. Vendor practices are examples, not PPIQ implementation claims or endorsements. OPC Foundation pages displayed Part 4 v1.05.07 during review. Source-specific parameters/defaults must not be transplanted into PPIQ without design acceptance.

| ID | Official source and scope used |
|---|---|
| E01 | **PTC Kepware — DataLogger Triggers.** Static interval, data-change, monitored-item group logging, band and update concepts. |
| E02 | **Inductive Automation — Ignition 8.1, Understanding Transaction Groups.** Gateway execution and subscription versus read-on-execution distinctions. |
| E03 | **Inductive Automation — Ignition 8.3, Tag Historian / Configuring Tag History.** Recording/deadband configuration; provider/version-specific historian behavior. |
| E04 | **Inductive Automation — Ignition 8.3, Store and Forward.** Applicable memory/disk buffering, recovery and forwarding responsibilities. |
| E05 | **OPC Foundation — Part 4 §5.13.1.2, Sampling interval.** Best-effort sampling and source/monitoring distinctions. |
| E06 | **OPC Foundation — Part 4 §7.22.2, DataChangeFilter.** Deadband and last-queued value semantics. |
| E07 | **OPC Foundation — Part 4 §5.13.1.6, Triggering model.** Linked sampled/queued items and reporting behavior. |
| E08 | **OPC Foundation — Part 4 §5.13.1.5, Queue parameters.** Queue size, latest-value behavior and overflow/discard. |
| E09 | **OPC Foundation — Part 4 §7.11.3, SourceTimestamp.** Source timestamp semantics distinct from receipt/ingestion. |
| E10 | **OPC Foundation — Part 3 §5.2.2, NodeId.** Stable source identifier versus namespace index/name assumptions. |
| E11 | **OPC Foundation — Part 4 §5.11.2, Read.** Requested maximum age and actual returned source data. |
| E12 | **Siemens STEP 7 V20 — Addressing tags in global data blocks.** DB absolute addressing, bit/byte/word/double-word and declared type. |
| E13 | **Siemens STEP 7 V20 — Basic information about operands.** Symbolic/absolute/optimized-access constraints. |
| E14 | **Beckhoff TwinCAT Scope — Oversampling recordings.** Source-local multi-sample capture and transport/timing pattern. |
| E15 | **Microsoft SQL Server — About Change Tracking.** Latest-state change identification, not all intermediate revisions. |
| E16 | **Microsoft SQL Server — About Change Data Capture.** Source changes and capture/retention requirements. |
| E17 | **Oracle Database 19c — LogMiner utility.** Source-specific redo/logging/decoding requirements. |
| E18 | **MySQL 8.4 — Binary Log.** Source change log and configured log behavior. |
| E19 | **PostgreSQL 16 — Logical Decoding Concepts.** Slots, re-delivery/restart and source retention responsibility. |
| E20 | **PostgreSQL 16 — Replication configuration.** Retained-WAL limits and source resource implications. |
| E21 | **PostgreSQL 16 — JSON Types.** JSONB representation is not original-byte preservation. |
| E22 | **PostgreSQL 16 — Table Partitioning.** Lifecycle/pruning and partitioned uniqueness limitations. |
| E23 | **PostgreSQL 16 — Row Security Policies.** RLS and privileged/owner behavior. |
| E24 | **PostgreSQL 16 — Disk Full.** Availability consequences of exhausted disk/WAL space. |
| E25 | **PostgreSQL 16 — System Administration Functions.** Table/index/total-relation storage measurement distinctions. |
| E26 | **OWASP — CSV Injection.** Spreadsheet formula hazards when exporting untrusted data. |
| E27 | **OPC Foundation — Part 2 §4.10, Application Authentication.** Certificates/trust and application identity. |
| E28 | **ISA — IC48, IEC 62443 security design training description.** Official zones/conduits/risk design framing only; not a full normative compliance review. |
| E29 | **OPC Foundation — UA .NET Standard repository.** SDK/library implementation option; not PPIQ certification. |

[U01]: #u01
[P01]: #p01
[P02]: #p02
[P03]: #p03
[P04]: #p04
[P05]: #p05
[P06]: #p06
[P07]: #p07
[P08]: #p08
[P09]: #p09
[E01]: https://support.ptc.com/help/kepware/features/en/kepware/features/datalogger/triggers.html
[E02]: https://www.docs.inductiveautomation.com/docs/8.1/ignition-modules/sql-bridge-transaction-groups/understanding-transaction-groups
[E03]: https://www.docs.inductiveautomation.com/docs/8.3/ignition-modules/tag-historian/configuring-tag-history
[E04]: https://www.docs.inductiveautomation.com/docs/8.3/platform/store-and-forward
[E05]: https://reference.opcfoundation.org/specs/OPC-10000-4/5.13.1.2
[E06]: https://reference.opcfoundation.org/specs/OPC-10000-4/7.22.2
[E07]: https://reference.opcfoundation.org/specs/OPC-10000-4/5.13.1.6
[E08]: https://reference.opcfoundation.org/specs/OPC-10000-4/5.13.1.5
[E09]: https://reference.opcfoundation.org/specs/OPC-10000-4/7.11.3
[E10]: https://reference.opcfoundation.org/specs/OPC-10000-3/5.2.2
[E11]: https://reference.opcfoundation.org/specs/OPC-10000-4/5.11.2
[E12]: https://docs.tia.siemens.cloud/r/en-us/v20/programming-basics/using-and-addressing-operands/addressing-operands/addressing-variables-in-data-blocks/addressing-tags-in-global-data-blocks
[E13]: https://docs.tia.siemens.cloud/r/en-us/v20/programming-basics/using-and-addressing-operands/basic-information-about-operands
[E14]: https://infosys.beckhoff.com/content/1033/te13xx_tc3_scopeview/182331147.html
[E15]: https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-tracking-sql-server?view=sql-server-ver16
[E16]: https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-data-capture-sql-server?view=sql-server-ver16
[E17]: https://docs.oracle.com/en/database/oracle/oracle-database/19/sutil/oracle-logminer-utility.html
[E18]: https://dev.mysql.com/doc/refman/8.4/en/binary-log.html
[E19]: https://www.postgresql.org/docs/16/logicaldecoding-explanation.html
[E20]: https://www.postgresql.org/docs/16/runtime-config-replication.html
[E21]: https://www.postgresql.org/docs/16/datatype-json.html
[E22]: https://www.postgresql.org/docs/16/ddl-partitioning.html
[E23]: https://www.postgresql.org/docs/16/ddl-rowsecurity.html
[E24]: https://www.postgresql.org/docs/16/disk-full.html
[E25]: https://www.postgresql.org/docs/16/functions-admin.html
[E26]: https://owasp.org/www-community/attacks/CSV_Injection
[E27]: https://reference.opcfoundation.org/specs/OPC-10000-2/4.10
[E28]: https://www.isa.org/training/course-description/ic48
[E29]: https://github.com/OPCFoundation/UA-.NETStandard

---

**End of advisory report.** The next authoritative state is created only by an approved edit to the owning chapters and backlog—not by treating this review, its example configuration or its reference checks as an executed product release.
