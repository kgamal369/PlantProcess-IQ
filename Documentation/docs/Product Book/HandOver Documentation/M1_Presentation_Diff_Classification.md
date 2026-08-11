# T-006 - CLASSIFICATION OF THE PRESENTATION DATABASE DIFF

**Task:** T-006 | **Milestone:** M1 | **Phase:** M1-P1
**Save as:** `Documentation/docs/Product Book/M1_Presentation_Diff_Classification.md`
**Input:** `docs/m1/evidence/presentation_db_diff.txt`, 2 August 2026, 18:59
**Rebuild:** clean. Provenance OK, genealogy invariant verified, weight guard confirmed re-enabled.

**33 differences: 15 schema objects and 18 row deltas.** Every one is classified below. Four need a query before the classification is final, and those queries are in the diff tool's new `-Mode Detail`.

---

## 0. THE HEADLINE - A WHOLE SUBSYSTEM EXISTS ONLY AS DATA

Nine of the fifteen objects form one coherent thing that is **not in source control anywhere**:

| Object | Kind |
|---|---|
| `ppiq_forensics.audit_ddl` | function |
| `ppiq_forensics.audit_wipe` | function |
| `ppiq_forensics.wipe_audit` + pkey | table |
| `ppiq_wipe_trap_genealogy_edges` | trigger |
| `ppiq_wipe_trap_import_batches` | trigger |
| `ppiq_wipe_trap_material_units` | trigger |
| `ppiq_wipe_trap_parameter_observations` | trigger |
| `ppiq_wipe_trap_quality_events` | trigger |
| `ppiq_wipe_trap_staging_records` | trigger |

This is a **forensic wipe-detection subsystem**: DDL auditing plus triggers that trap mass deletion on the six core canonical tables. Somebody built it in the live database, almost certainly after a data-loss incident, and it never became a migration.

**`ppiq_forensics.wipe_audit` holds 18 rows.** Eighteen events were trapped. Whatever those are, they are the reason this subsystem exists, and reading them is the first thing to do - they may name a defect that is still open.

**Classification: PRODUCT FIX.** It protects canonical data, it is not presentation-specific, and it should be running at every customer. One numbered migration in `Backend/database/scripts`.

**And a caution before writing it.** A trigger on `material_units`, `quality_events`, `parameter_observations`, `genealogy_edges`, `import_batches` and `staging_records` fires on the projection path - the hot path. The genealogy weight guard cost ten minutes on 35,906 rows this afternoon. Before this becomes a migration, read `audit_wipe` and confirm it is a **statement-level** trigger, or it will make every import slower at every customer. If it is row-level, that is a defect to fix in the migration, not to inherit.

---

## 1. THE OTHER SIX OBJECTS

| Object | Rows in live | Classification |
|---|---:|---|
| `public.ppiq_catalog_audit` + pkey | 37 | **PRODUCT FIX** - an audit table belongs at every customer |
| `public.ppiq_purge_audit` + pkey | 21 | **PRODUCT FIX** - same |
| `public.ppiq_layout_backup` | 37 | **DECIDE - see below** |

**`ppiq_layout_backup` is the one to argue about.** A table whose name says *backup*, holding 37 dashboard layouts, created outside source control. Two readings:

- It is a deliberate safety net for layout edits, in which case it is a product feature and needs a name that says so, a retention rule, and a migration.
- It is debris from a one-off rescue, in which case it is Rule 4 material and it should be dropped, not migrated.

Thirty-seven rows against 42 widget definitions suggests a snapshot taken once, not a living feature. **My reading: debris. Recommend dropping it rather than migrating it** - but this is a decision, not a fact, and it needs your call.

---

## 2. ROW DELTAS - WHAT ACTUALLY MATTERS

### 2.1 The largest gap in the diff, and it is a presentation risk

| Table | Live | Scratch | Delta |
|---|---:|---:|---:|
| `public.ml_outcome_values` | 195,221 | 91,839 | **-103,382** |

**More than half the outcome values in the presentation database do not come from the rebuild.** They were produced by running the engine against the live database over the past fortnight, and that never became a scripted step.

`ml_feature_store_refresh_runs` confirms it: 11 runs in live, **1** in scratch.

**Why this matters more than its row count.** 195,221 is the number the readiness probe reports and the number the Findings and ML Readiness pages compute from. **Rebuild before the demonstration and you lose 53 percent of the evidence base**, silently, with every page still rendering. The readiness gate may even flip from Ready to Partial on some dimension.

**Classification: PRESENTATION DATA, but it must become a scripted step.** Add a feature-and-outcome refresh to the rebuild after step 1b, and assert the resulting count in step 7. The law holds - data may be presentation-only, but it may not exist only in a database nobody can rebuild.

### 2.2 The assistant index

| Table | Live | Scratch |
|---|---:|---:|
| `canon.assistant_chunk` | 25 | 0 |
| `canon.assistant_index_run` | 6 | 0 |

Derived, not authored. **Classification: REGENERATE.** Add a reindex call to the rebuild rather than seeding rows.

Note the size: **25 chunks.** That is a very small corpus, and it is consistent with the earlier finding that `kb_items` reads 0 on both profiles. The assistant has almost nothing to retrieve, which is the precondition the page-and-widget chunk family task addresses.

### 2.3 Definitions that exist only in live - ENUMERATE BEFORE CLASSIFYING

| Table | Live | Scratch | Delta |
|---|---:|---:|---:|
| `dashboard_definitions` | 16 | 12 | **-4** |
| `dashboard_widget_definitions` | 42 | 38 | **-4** |
| `ppiq_mapping_versions` | 83 | 78 | **-5** |
| `source_dataset_definitions` | 4 | 3 | **-1** |

**Four dashboards, four widgets, five mapping versions and one dataset definition exist only in the live presentation database.** Somebody created them through the interface or by hand, and no script reproduces them.

This is exactly the case the task was written to find, and the governing rule says read it twice: a seeded dashboard is presentation data, but **a corrected widget definition, a repointed outcome key or a fixed mapping is a product fix wearing data's clothes.**

**I cannot classify these without seeing them.** Run `-Mode Detail` and the classification completes.

**A prediction, so it can be wrong on the record:** I expect at least one of the four widgets to be a manual repair to a binding that fails in the seed script - which would make it a product fix, and would also mean the seed script still contains the defect it was repairing.

### 2.4 Runtime history - accepted drift, and my script has a design flaw here

| Table | Live | Scratch | Delta |
|---|---:|---:|---:|
| `audit_log_entries` | 5,720 | 3,600 | -2,120 |
| `auth_refresh_tokens` | 1,443 | 1,069 | -374 |
| `job_log` | 130 | 120 | -10 |
| `job_run_histories` | 1,267 | 743 | -524 |
| `read_model_refresh_runs` | 915 | 391 | -524 |
| `ml_correlation_compute_runs` | 449 | 384 | -65 |
| `ppiq_forensics.wipe_audit` | 18 | 0 | -18 |
| `ppiq_catalog_audit` | 37 | 0 | -37 |
| `ppiq_purge_audit` | 21 | 0 | -21 |
| `ppiq_layout_backup` | 37 | 0 | -37 |

These are records of things having happened. They cannot match and should not be seeded - seeding a fake audit trail would be worse than the mismatch.

**But this exposes a defect in my own diff tool.** `-Mode ReVerify` demands an **empty** diff, and these ten tables can never be empty-diff. **T-006 was unachievable by construction.**

**Fix:** a checked-in ignore list, `docs/m1/presentation_diff_ignore.txt`, one table per line with a reason - the same shape as the Rule 2 prefill allowlist, and for the same reason. A table is compared **by default**; ignoring one is a reviewed decision, so a new table nobody classified shows up as a difference rather than disappearing.

`ppiq_forensics.wipe_audit` goes on that list **only after its 18 rows have been read.**

---

## 3. THE WORK THIS PRODUCES

| # | Item | Where | Est. |
|---|---|---|---:|
| 1 | Read the 18 wipe-audit rows and the DDL audit entries | investigation | 1 h |
| 2 | Migration: the `ppiq_forensics` subsystem, after confirming the triggers are statement-level | `Backend/database/scripts` | 3 h |
| 3 | Migration: `ppiq_catalog_audit` and `ppiq_purge_audit` | same migration | included |
| 4 | Decide `ppiq_layout_backup` - migrate or drop | decision | - |
| 5 | Rebuild step: feature and outcome refresh, with the count asserted in step 7 | `scripts/demo` | 3 h |
| 6 | Rebuild step: assistant reindex | `scripts/demo` | 1 h |
| 7 | Classify and script the 4 dashboards, 4 widgets, 5 mapping versions, 1 dataset | after `-Mode Detail` | 3 h |
| 8 | The diff ignore list, as data with a reason per line | `docs/m1` | 1 h |
| | **Total** | | **12 h** |

**T-006 is estimated at 8 hours and this is 12.** The extra four are the forensics migration, which nobody knew existed until this afternoon. That is what the task was for.

---

## 4. WHAT IS ALREADY PROVEN

The repair worked. The provenance step reports OK, the genealogy invariant is verified after the suspended update, and the weight guard is confirmed re-enabled - all three as counted steps that would have failed the run. The ten-minute step is gone.

`ONLY IN SCRATCH : 0` is worth stating plainly: **nothing in source control is missing from the live database.** The drift runs one way only. That is the good news in this diff, and it means the fix list above is complete rather than a first instalment.
