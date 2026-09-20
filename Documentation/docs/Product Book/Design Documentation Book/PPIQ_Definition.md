# PlantProcess IQ - Definition and Design Book Control

| Item | Value |
|---|---|
| Functional Design | **v4.11.1** |
| Execution Backlog | **v2.24.0** |
| Current edition | **17 September 2026** |
| Functional authority | Chapters 1–6 v4.11.1 + database standards |
| Execution authority | `PPIQ_Backlog_v2.24.0_17Sep2026.xlsx` |

## A1. Current authority and inventory

**Functional Markdown authority package:** Chapters 1–6 v4.11.1, Database Naming Standard v1.2, Product Analysis & Evaluation Standard v1.2, Identity/Topology v5 reference, current database dictionary workbook and Backlog v2.24.0. The two illustrated Word editions are **derived visual companions** and are not claimed present in this package; T-265/T-266 own their regeneration from this functional authority. Their absence does not block backend/runtime workers from consuming the frozen functional contracts.

The six Markdown chapters in this package are the current **functional execution design**. v4.11.1 integrates the Industrial Integration contract across Chapters 1–6 and supersedes v4.11.0/v4.10.3 for current design interpretation. Backlog v2.24.0 remains the execution version because the 17-Sep pass corrects status/evidence/dependencies/acceptance and design references without adding or retiring another task.

Registered current files supplied in this package:

1. `PPIQ_Definition.md`
2. `PPIQ_Chapter1_Marketing_and_Sales_v4.11.1.md`
3. `PPIQ_Chapter2_Technical_Overview_v4.11.1.md`
4. `PPIQ_Chapter3_General_Technical_Function_Description_v4.11.1.md`
5. `PPIQ_Chapter4_Specific_Technical_Function_Description_v4.11.1.md`
6. `PPIQ_Chapter5_Tutorial_User_Journey_v4.11.1.md`
7. `PPIQ_Chapter6_Infrastructure_Website_Administration_v4.11.1.md`
8. `PPIQ_Database_Architecture_and_Naming_Standard_v1.2.md`
9. `PPIQ_Database_Architecture_and_Data_Dictionary_v4.11.1.xlsx`
10. `PPIQ_Product_Analysis_and_Evaluation_Standard_v1.2.md`
11. `PPIQ_Identity_and_Topology_v5_28Aug2026.md` (environment snapshot only; historical task/HEAD statements are non-authoritative)
12. `PPIQ_Backlog_v2.24.0_17Sep2026.xlsx`

The illustrated Word editions were **not supplied as editable originals in this session**. They are visual derivatives only and do not block Worker execution: current functional authority is the v4.11.1 Markdown contract. T-265/T-266 retain visual/illustrated synchronization acceptance. Old Word bytes must not be relabelled as v4.11.1.

## A2. Authority order

1. Design chapters/database design = target.
2. Runtime/DB/browser proof and executed tests = implemented reality/evidence.
3. Current repository = implemented source reality.
4. Backlog = execution order, owners, status and closure record.
5. Evaluation Standard = assessment method, not implementation proof.

Valid closed tasks remain closed absent concrete regression. Later widened work is bounded to its named owner; it does not retroactively erase a producer closure.

## A3. v4.11.1 correction scope

- Ch2 now owns B3 **Prepare Acquisition**, B4 **Acquisition Runs**, Release-1 industrial acquisition and L1.8 Core classification.
- Ch3 page addenda are integrated into the page contracts; B1/B4 no longer own scheduling; industrial endpoint families are explicit.
- Ch4 runtime semantics remain the execution authority for periodic/on-change/triggered policies, consistency, durable acceptance, fencing, bounded queues/replay and continuous-session/finite-batch behavior. Toolbox citations use stable section/group references; brittle line-number citations are retired.
- Ch5 teaches the unified acquisition path inside T1/T2 rather than creating a ninth tutorial.
- Ch6 sizes event/payload rate, spool/offline coverage, source retention, replay drain and qualification.
- Database Standard v1.2 adds `RETAINED_SOURCE_AUTHORITY` for governed accepted source-shaped records.

## A4. M2 planning ruling

The approximately-one-month M2 statement is a **planning intent, not a forecast**. v4.11.x added eight Critical Release-1 owners (T-268..T-275) and several current tasks require re-estimation. No date is moved and no Industrial Integration scope is silently deferred to M3. The Industrial Integration source preflight completed GREEN on 17-Sep at HEAD `db0b0f84ec721f8adff1da14d4a62762200a22d6`, producing a ranked 198-file read-only export. LOW/BASE/HIGH calibration remains planning work, **not a worker start gate**. No new delivery forecast is asserted until that calibration is accepted.

## A5. Current execution safeguards

- T-100 is CLOSED GREEN / COMMITTED at `e7ebc56341c371ce7b617585dce7f42d9a255f61`.
- T-243 relationship-publication + ambient-transaction closure is CLOSED GREEN / COMMITTED at `33aca26a1063cc78fdc3900bb0432886fe496c87`.
- T-262 is CLOSED GREEN / COMMITTED at `db0b0f84ec721f8adff1da14d4a62762200a22d6`.
- T-106 is **CLOSED GREEN / released** at the recovered final short anchor `a0a5618d`. Its widened integration proof is complete; the remaining registered Transformation executor acceptance belongs to T-261, not to a hidden T-106 remainder.
- T-233 remains **In Progress**; latest Source Time runner evidence is RED with byte-exact rollback and no commit, so no closure is claimed.
- T-261 Layer-A engineering exists as intentionally preserved **uncommitted postimages**. Do not reset or treat those bytes as orphan dirt; commissioning/closure remains T-261 work.
- Repository screenshot/preflight at HEAD `db0b0f84ec721f8adff1da14d4a62762200a22d6` shows 40 pre-existing working-tree entries. The visible JobExecutor/Transformation execution/evidence files belong to the T-261 executor lane and must remain preserved until that lane is either committed or explicitly rolled back by its owner. Unseen entries are not guessed into a task; exact path ownership requires the full status list.

## A6. Repository filename law

No repository implementation/test/fixture/source/SQL filename may carry a Task ID. Task IDs belong in backlog/evidence/commit text only.

## A7. 17-Sep worker start and working-tree ruling

The 40 pre-existing dirty entries observed after T-262 are **not a reason to block all lanes**. The visible executor/projection family (`VisualMapperEndpoints.cs`, `TransformationCompiler.cs`, `JobExecutor*`, `JobExecutionContext`, `JobExecutionDiagnostics`, `ITransformationCanonicalWriter`, `TransformationExecutionPlan`, `TransformationProjectionJobExecutor`, `JobRunBlockEvidence`, `JobRunBlockStatus` and focused tests) belongs to **T-261 in-progress postimages**. This classification is backed by the earlier T-261 edit-surface/runner history that created the same executor/compiler/evidence seam. Unseen dirty paths are not guessed into a task.

Immediate routing from this authority package:

1. **Worker 1:** close the already-in-progress T-233 Source Time persistence/certification; then start T-268. Do not abandon T-233 into another half-finished lane.
2. **Worker 2:** finish T-261 from the preserved postimages first; do not reset them and do not absorb them into T-262. After T-261 closes, continue the next eligible T-245/T-254/T-266 work according to dependency/path locks.
3. **Worker 3:** start T-224, then T-225; continue T-107 as required before T-275. T-275 starts only after its completion prerequisites are satisfied.

Before any commit from the dirty tree: exact path census, preimage hashes, owner mapping, unrelated-byte fingerprint and exact owned staging are mandatory. No worker cleans another worker's bytes merely to obtain a clean tree.

Known current commit frontier used by this package:

- T-100: `e7ebc56341c371ce7b617585dce7f42d9a255f61`.
- T-106: released short anchor `a0a5618d` (full SHA not recovered in the supplied evidence set; do not invent it).
- T-207 corrective: `3908c6f7eee3167902a9cb2ad39ce215d2fe0182`.
- T-243 transactional publication: `316383913d3d8254003639274a098f829ecc1ea2`; ambient correction: `33aca26a1063cc78fdc3900bb0432886fe496c87`.
- T-252: `50c282ad5cb301a6422273599d6d6a05e70f71d4`.
- T-256: `36de9370765183e76227869372fbcd14b43dd066`.
- T-262 producer: `86e1b677212195f191697ecdca7e9441b3dbde33`; final closure/current observed HEAD: `db0b0f84ec721f8adff1da14d4a62762200a22d6`.

Where only a short commit anchor is recovered, the backlog records it as short evidence rather than fabricating a 40-character SHA.
