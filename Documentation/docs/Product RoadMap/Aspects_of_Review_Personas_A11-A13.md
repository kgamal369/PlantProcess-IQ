# Aspects of Review - Personas A11-A13 (Amendment to Part A)
**20-Jul-2026 | Origin: Karim's mandate, 20-Jul session | Status: ADOPTED into the audit persona catalog**
**ID note:** mandated as "A7/A8/A9"; renumbered A11-A13 because A7, A9, A10 were consumed by the 12-Jul audit and the frozen-ID law (Backlog v23 obligation 1) forbids recycling. Content unchanged from the mandate.

---

## A11 - The UI/UX Auditor
Evaluates the product as a NON-PROGRAMMING plant user experiences it.
Scope: user happiness and journey clarity; intuitive navigation across the 15 steps; correct Qlik-class generic dark-industrial visual language (token set, shared palette, zero page-local schemes); and the STRICT implementation of the four Low-Code surfaces - drag-and-drop components, live displacement, edge-resize, min/max framing, interactive wiring/canvas where specified, wizard completeness, and whether each low-code act is achievable without writing code.
Evidence standard: browser-verified walks with screenshots; every "low-code" claim demonstrated by a naive-user click path, or graded as forms honestly.
Known day-one findings (20-Jul): UI-2 grid canvas real but [B]-unverified, Add-widget entry unmounted; UI-1/UI-3 are forms - no node-wiring foundation exists in the dependency tree (react-grid-layout only); S5 components (conditional tables, list, date-range) absent.

## A12 - The AI & Engine Auditor
Evaluates the Engine as a statistical and AI system, not as endpoints.
Scope: the Statistics and AI+ML method toolboxes (coverage vs the registry: Pearson live; Spearman/chi-square/ANOVA committed); correlation correctness (FDR control, effect sizes, honest nulls, dedup-to-latest-run); how the engine DIVES into the data (grain handling, genealogy attribution of heat-level parameters to coil-level outcomes, window anchoring - wall-clock vs dataset-max, a live 20-Jul finding); readiness-gate integrity (never weakened, reasons persisted and explainable); LLM/assistant integration (retrieval grounding, citation resolution, refusal-first, read-only toward the Engine); and multi-threading/scale (bounded-parallelism executor, 100+ concurrent jobs, statement timeouts, telemetry).
Evidence standard: known-answer tests against planted relations (R1-R17, seed=42); a blocked run must name its blocking dimension from the database alone.
Known day-one findings (20-Jul): window anchored to now() blinds historical datasets; results_v2 FORCE-RLS with NULL tenant_id hides all findings from the application; API/database binding mismatch produced 375 blind runs.

## A13 - The Infrastructure Engineer
Evaluates whether the platform physically carries its promises.
Scope: hosting and deployment topology (single VM -> PgBouncer -> LB'd + replica per the sizing doctrine); server sizing against the declared volumes (Small ~750k obs/yr, Medium ~7.5M, Large ~60M); database scaling (partitioned observations/features, incremental feature refresh, index discipline, RLS cost); compute headroom for the engine job classes (import/analysis/ML pools) under 100+ defined jobs; backup/restore and rebuild reproducibility (the M2-18 command as the standard); container/source-emulation separation (no source data inside the product DB); secrets, certificates, and network exposure.
Evidence standard: measured numbers from pilot-class telemetry or load rigs - never estimates presented as measurements; every sizing claim in the Doctrine 6 table traceable to a run.
Known day-one findings (20-Jul): dump_store/src_* Arch-B residue inside product DBs (Amendment 6 eradication); legacy plantprocessiq database; pgvector unavailable in the running instance.

---

*These personas join the existing catalog (A1-A10, historical audits immutable). First engagement: the post-demo audit. Each persona's findings feed Backlog v23 with new IDs, Origin = the audit report.*
