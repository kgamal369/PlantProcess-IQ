# PlantProcess IQ — V1 Runbook · Sat 27 Jun → Wed 1 Jul 2026
## Get the 7-step journey to walk end-to-end, stable, demo-ready for Procurement

> **The rule:** the journey *is* the test. You are not building pages (they exist, 350–1,126 lines each) — you are walking the chain and fixing whatever breaks the hand-off between stages. Every day ends with the stack deployable and one more seam proven. **Front-load the risk:** the data chain (seams 2→5) is verified on Day 1, so a break has four days of runway, not zero.
>
> **Audience reminder:** this demo is for **Purchasing/Procurement** — the goal is to amaze them into granting the technical meeting. Run the registered **8 demo sources only**; the hardcoded Stage-2 is the accepted V1 shortcut (eradicated in V2). Do not connect a novel source on stage — that path breaks (seam 5) and is V2 work.

---

## PRE-FLIGHT (once, Day 1 morning) — stand up a clean stack

Canonical values you'll check against: tenant `00000000-0000-0000-0000-000000000001` · golden coil **C-0044170** · transition attribution **0.70 / 0.30** across **H-3361 / H-3362** · demo dataset **8 sources** (heats=630, coils=5600, surface_defects=2132) · `material_units` keyed by `site_id`.

```powershell
# from C:\Workspace\PlantProcess-IQ  (ppiq.ps1 lives in deploy\scripts)
.\deploy\scripts\ppiq.ps1 up            # main stack
.\deploy\scripts\ppiq.ps1 up-sources    # demo source DB containers
.\deploy\scripts\ppiq.ps1 migrate       # EF update + all numbered SQL + dev Ed25519 license
.\deploy\scripts\ppiq.ps1 seed          # capture the 'seed exit=' line -> must be 0
.\deploy\scripts\ppiq.ps1 status        # all green?
```

**Pre-flight gate:** `migrate` and `seed` both exit 0; `status` shows the API and DB healthy. If `seed exit` ≠ 0, stop and fix the seed before anything else — nothing downstream can be trusted on a dirty seed.

---

## DAY 1 · Sat 27 Jun — Stand up + de-risk the data chain (seams 2,3,5)

**Goal:** prove that connecting → staging → canonical actually produces data, end-to-end, *today*. Plus the single highest live-crash risk (Caddy routes).

**Do — drive the two-stage cycle (HMI first, SQL as ground truth):**
- HMI: open **Admin → DB Configuration**, register/select all 8 demo sources, run the connection test (green), pick the tables.
- HMI: **Admin → Importing Data** → run **Provision baseline**, then **Full cycle** (Stage-1 + Stage-2).
- Ground-truth fallback (psql against `ppiq_app`) — these function names are verified in the code:

```sql
-- register the 8 demo dump sources (idempotent)
SELECT public.ppiq_register_dump_source('MELTSHOP_PG','src_meltshop_pg','heats',ARRAY['heat_no'],'source_updated_at_utc',2,30);
-- ...(the other 9 registrations from ProvisionBaseline)...

-- run the whole pipeline once
SELECT * FROM public.ppiq_run_two_stage_full_cycle('Runbook', 50000, 120, 1);

-- VERIFY canonical actually populated
SELECT count(*) AS material_units FROM public.material_units WHERE is_deleted = false;            -- expect > 0
SELECT material_key, material_type FROM public.canonical_material_units WHERE material_key = 'C-0044170';  -- expect 1 row
```

**Stability (highest crash risk):** make the Caddy routes persistent so a restart can't drop the demo (V1-P1). Confirm the live compose project is the single `plantprocessiq` project, not an orphan.

**Day-1 gate (the most important of the week):** `ppiq_run_two_stage_full_cycle` returns canonical rows; `material_units` count > 0; **C-0044170 resolves**. If this is red, you've found the worst-case break with four days to fix it — escalate it to me with the function output.

---

## DAY 2 · Sun 28 Jun — The two verify-needed HMI seams (4 mapper, 6 dashboard)

**Goal:** close the two seams I could not confirm statically — the mapper reading staging, and dashboards bound to the *imported* canonical data.

**Do:**
- **Seam 4 — no-code mapper (J4):** open **Admin → Schema Configuration** + the **Canonical Schema Mapping** panel (and `V5NoCodeMapperPage`). Confirm it lists the **real demo staging tables and columns**, previews rows, accepts a join across two dump tables, and rejects bad SQL with a typed safe-SQL error. *If the mapper shows nothing,* the UI isn't bound to the staging schema — capture which call returns empty.
- **Seam 6 — dashboard on imported data (J6):** create a page (Page Builder), drag a widget (Widget Builder wizard), bind it to a **canonical view** via the widget script layer, save. Then **re-run the import** and confirm the widget's numbers change. *This is the proof that dashboards read the canonical views Stage-2 fills — not pre-seeded tables.*

**Day-2 gate:** the mapper lists real staging metadata and previews; a saved widget renders imported rows **and visibly changes when you re-import**. If the widget doesn't change on re-import, seam 6 is bound to seed data → fix the binding (this is the "watch the data flow in" moment that sells).

---

## DAY 3 · Mon 29 Jun — Close the journey ends (J1, J7) + dead-button sweep

**Goal:** the first and last steps, then eliminate every dead end on the path.

**Do:**
- **J1 — auto-login:** cold-load the app; confirm it lands authenticated as sysadmin on a populated home, no login prompt, no console errors.
- **J7 — analysis on imported data:** from **Advanced Analysis / Inspection Jobs**, configure an inspection (defect/downtime/KPI + duration), run it on the imported canonical data, read a **grounded suspected-contributor** result; confirm the assistant cites evidence and renders no uncited number.
- **Dead-button sweep:** walk J1→J7 clicking **every** control on the path; run the dead-button scanner; enumerate **all** dead/placeholder buttons in one list and fix them in one pass (preventive-maintenance mandate — don't fix one at a time).

```powershell
.\deploy\scripts\ppiq.ps1 e2e      # the end-to-end harness (boot-under-Playwright)
node Frontend\PlantProcess.Web\scripts\dead-button-scan.mjs
```

**Day-3 gate:** J1 lands clean; J7 returns a grounded result with population + method shown; **zero dead buttons** on the journey path (scanner clean + manual walk clean).

---

## DAY 4 · Tue 30 Jun — J-WALK under faults + record the backup video + finalize pack

**Goal:** prove the whole chain survives a clumsy click, and bank insurance against a live glitch.

**Do:**
- **J-WALK:** on a clean stack, walk all 7 steps as one continuous flow. Then **induce faults** at each step and watch for graceful degradation: a bad login, a role/tier-gated control, a container restart mid-session, a brief DB blip. Nothing may white-screen or throw an uncaught 500; it must recover when the fault clears.
- **Record the dry-run video** (your V1-20 cut sheet) — a clean 2–3 minute walk of J1→J7. This is your fallback if anything glitches live.
- **Finalize the pack:** deck + one-page brief + the **ROI slide with your real per-plant price** (still the one open input) + the pilot offer.

**Day-4 gate:** the full walk completes with zero dead-ends; each induced fault degrades gracefully and recovers; the **recorded video exists**; the deck/ROI/offer are done.

---

## DAY 5 · Wed 1 Jul — Buffer + smoke + the pitch

**Do (morning):**
```powershell
.\deploy\scripts\ppiq.ps1 status
SELECT * FROM public.ppiq_run_two_stage_full_cycle('PreDemo', 50000, 120, 1);   -- one clean cycle
.\deploy\scripts\ppiq.ps1 e2e                                                    -- one clean walk
```
- Confirm the video backup is open and ready. **Freeze the stack** — do **not** restart Caddy or regenerate `.env` after this point.

**Day-5 gate:** green status + clean full-cycle + clean e2e; video ready; **deliver the pitch.**

---

## Demo-day operating discipline (tape this to the monitor)

- Run from the **frozen** stack; do not restart Caddy or touch `.env` during the demo window.
- If a step glitches live, **cut to the recorded video** for that step and keep narrating — never debug on stage.
- Stay on the **8 registered demo sources**. If asked "can you connect *our* source live?", answer honestly: *"That's the V2 generic-mapper milestone — today I'm showing the full workflow on a representative plant dataset."* (True, and it sets up the next meeting.)
- The close, every time: *"Suspected contributor, not guaranteed root cause — read-only, no OT control."*

---

## Sequencing logic (why this order)

Day 1 attacks the **only break that can't be worked around** (no data in canonical → nothing downstream testable), so a failure surfaces with maximum runway. Days 2–3 close the **verify-needed seams** and the **dead-button** risk that a Procurement viewer *will* trigger by clicking around. Day 4 hardens the **continuous walk** and banks the **video** so the live demo can never fully fail. Day 5 is pure buffer. If you slip, the safe cut is to **demo from the video** for any unproven seam while you keep fixing — the pack, the website, and the recorded walk alone can carry the Procurement meeting.

*Open inputs only you can supply: the real per-plant price (ROI slide), and recording the dry-run video. Everything else is verify-and-harden on code that already exists.*
