# PPIQ Product Roadmap v9
**15-Jul-2026, 15:10** | Derives from and cites `concept.md v1.1` (apply the Amendment Sheet first)
Supersedes: Roadmap v8 (10-Jul, orphaned - cited Backlog v21 whose ID namespace v22 recycled)
Companion: Backlog v22 (38 tasks / 225h) **plus the v23 obligations in section 5**

---

## 1. Position (15-Jul, 15:00 - evening before the second customer meeting)

The platform is structurally demo-ready and data-empty. Every gate is green (60/60
architecture tests incl. the new `uiConformanceRatchet`; CI truth-gate suite verified
against the live Jenkinsfile; 0 dead handlers across 142 files; 0 mojibake). The
Rule-1 visible surface is clean (30 customer-visible strings, engine messages,
DB history, chrome hedges - all removed with evidence, 15-Jul). All four sources are
connected, schema-verified, and mapped on paper; the Oracle discovery defect is
root-caused (Schema Name = PPIQ_SRC). **What has NOT happened as of this writing:
the imports (Runsheet Phases A-E).** Until they run, journey steps 5-15 render
empty states. The runsheet is the critical path; everything else in this document
is sequenced behind it.

## 2. Journey scoreboard (concept v1.1, 15 steps - honest grades)

| Step | State 15-Jul 15:00 |
|---|---|
| 1 Connect | [P] 6 profiles; Oracle schema fix known, pending the 2-field UI edit |
| 2 Certify read-only | [P] enforced + negative-test source retired |
| 3 Register datasets | [ ] Runsheet Phase A/B/C/D - tonight |
| 4 Map (no-code) | [P] mechanism proven (M1-03/M1-04); mappings authored per runsheet |
| 5 Import + monitor | [ ] pending Phase B+ ; job_log sanitized; monitor verified |
| 6 Genealogy | [P] chain proven with live keys (H-26014 -> SL-60105 -> C-700394); edges pending import |
| 7 Dashboards | [B] render; empty until Phase B/D |
| 8 Author analysis | [P] M1-05 surface, 13/13 |
| 9 Readiness gate | [P] fires honestly; message de-demo'd (was: "demo learning") |
| 10 Findings | [ ] pending Phase E run - expected R1 9.3x, SCRATCH null |
| 11 Evidence drill | [B] |
| 12 Alerts | [B] UI-4 shipped; needs live events |
| 13 Actions/value | [B] |
| 14 Supervisor | [B] v0 (report+honesty+monitor row); no schedule/tuning/provenance - M2 KEYSTONE |
| 15 Assistant | [B] wired endpoint group registered; reindex after Phase D |

## 3. Closed since v8 (10-Jul -> 15-Jul, evidence on file)

- M1-08 route canonicalization + UI professionalisation; M1-11 walk instrument (v2.1 pending 3 corrections)
- Purge C.1/C.2 (38k seed rows) + 15-Jul residue sweeps S1-S9 (schema-view catalog rows,
  engine golden/demo messages incl. live function redefinition, LINQ log leak, orphan
  taxonomy + fixture edges)
- Gate family completed: T11 closed (StandardButton conversions), noMojibake tree-wide,
  test-pin de-demo, uiConformanceRatchet installed (falsify-once run: PENDING - owner Karim)
- Source realization: fleet identified as the running dataset; ports corrected
  (15432/13306/13307); PPIQ_SRC schema fix; taxonomy views live on all applicable
  sources; parsytec catalog discovered (Route-A premise revised, concept Amendment 4)
- Money-slide corrected: R1 = 9.3x (9.5x is R3)

## 4. M1 remainder (before/around the meeting)

| ID | Task | State |
|---|---|---|
| RS-A..E | Runsheet Phases A-E (imports + engine run) | CRITICAL PATH - tonight |
| M1-V | Rehearsal walk x2 = browser-verification pass for all 15-Jul UI packs | tonight |
| M1-W | Walk doc v2.1 (login route/token field, rail order, taxonomy premise) | 10 min |
| M1-X | Falsify the ratchet once; commit baseline+test together | 1 min |
| M1-Y | Deck: 9.3x | 1 min |
| CUT-LINE | D3/D5 pack (InfoTips + StandardPageHeader on 4 core pages) | only if rehearsal leaves room |

## 5. Backlog v23 obligations (structural - this is how work stopped disappearing)

1. **Freeze the ID namespace.** v22 recycled v21 IDs; 15h of Rule-1 keystone work
   (v8 M2-02/M2-03) silently vanished in the renumbering. From v23: IDs are permanent;
   new work gets new IDs; an explicit Origin column remains mandatory.
2. **Restore the eradication epic (was M2-02/M2-03), scoped at the REPO:**
   delete installer scripts 110/111/140/142/665 + `seed/*demo*` + `source-systems/synthetic_*`;
   DROP the 3 orphan `v_phase3_dump_*` views + `demo_source_connection_presets` +
   `ppiq_demo_*` + `ppiq_mostly_green_task_closure`; delete `CustomerDemoReportEndpoints.cs`
   + `TwoStageImportEndpoints.cs`; retire the `/demo*` route family from the access matrix;
   delete the four unrouted pages (V5NoCodeMapper, V5OutboundI18nMobile, MaterialAnalyticsPages,
   CostAssumptionManagementPage); drop the legacy meltshop fixture tables in the source.
   **Then build the migration-path gate and falsify it once.** (~15-18h)
3. **UI conformance burn-down (measured):** baseline frozen 15-Jul at D1=161 raw
   controls / D2=112 inline styles across 37 files, ratchet-enforced monotonic.
   Click-order packs: AdminDbConfigurationTab(18) -> SourceImportPrep(9+13) ->
   AuthorMapping(6) -> AnalysisJobConfig(18+25) -> widget builder(20) -> Alerting(10)
   -> remainder. Each pack ends with -RegenerateBaseline. (~12-16h)
4. **Tooling hygiene:** consolidate the drifted duplicate scanners (audit generator x2,
   dead-button-scan x2); audit generator gets self-exclusion + tools/docs/backup
   excludes + MaskSecrets default TRUE (the 15-Jul package shipped ~104 plaintext
   credential-shaped strings). (~3h)

## 6. M2 (unchanged priorities, updated grades)

- **P0 KEYSTONE - THE SUPERVISOR full loop** (schedule, tuning actions, adjustment
  provenance rows incl. the missing storage table). v0 exists; the gap is the loop. (24h)
- P0 - Eradication epic (section 5.2).
- P1 - Per-view Loading jobs; observations breadth (HSM_PASSES-class datasets as
  scheduled imports); job-logger sanitization at source (S4 fixed data, not producer).
- P1 - Test isolation: Phase3GoldenThread fixtures write PPIQ_P3_SEED edges into
  ppiq_app - point at PPIQ_TEST_PG_CONNSTRING only.
- P2 - default-demo tenant rename (source-literal gate first); DEMO_USER->SMOKE_USER
  const renames; enum display-label normalization (Level2, SyntheticGenerator via
  REF_BASELINE retire-or-relabel decision); AssistantChat citation renders raw id;
  dead demo-mode CSS deletion; `_phase9_standardbutton_dedupe_backup` relocation;
  StandardButton dual-export consolidation.
- Deferred register carried intact: production Ed25519 keypair (Option 3); two missing
  CI truth-gates (seed NOT-NULL coverage; no-two-scripts-CREATE-same-table); Hetzner/
  Spamhaus mail remediation; backend mojibake in 6 .cs files; Recommendations/
  ValueRealization canned-request backend.

## 7. Standing rules of this roadmap

Evidence before cure. No gate trusted before a recorded red. IDs never recycled.
Audits are superseded, never edited. The installer is Rule 1's largest open violation
until 5.2 lands - every fresh install today still creates demo schemas; local and
server cleanliness is an artifact of manual purges, not of the product.
