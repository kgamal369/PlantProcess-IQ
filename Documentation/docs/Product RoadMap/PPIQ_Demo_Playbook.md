# PPIQ Demo Playbook - M1-11 + M1-14 + M1-15
**The single document you run the meeting from. Fill the [ ] grades tonight during rehearsal.**

=====================================================================
## PART 1 - M1-11: Certification checklist (walk once, grade each)
Grades: [P] pass / [B] built-but-issue (has a spoken sentence) / [X] broken (FIX or REMOVE from path)
=====================================================================

| # | Surface | Grade | Screenshot? | Spoken sentence if [B] |
|---|---|---|---|---|
| 1 | Login | [ ] | | |
| 2 | Connections (2 Oracle profiles, PPIQ_SRC discovered - M1-19) | [ ] | | |
| 3 | Prepare Import + live registration (M1-28) | [ ] | | |
| 4 | Importing / Jobs Monitor | [ ] | | |
| 5 | Genealogy: pick a coil -> slab -> heat -> provenance | [ ] | | |
| 6a | Command Dashboard / PRODUCTION_OVERVIEW renders (8 widgets) | [ ] | | |
| 6b | Switch BAR->PIE->TABLE on one widget (fixed 08:41) | [ ] | | |
| 6c | Global filter -> all widgets requery (fixed 08:41) | [ ] | | |
| 6d | Click a materialCode bar -> cross-filter | [ ] | | |
| 6e | Drag + resize a widget + Save layout + reload persists | [ ] | | |
| 6f | 2 more workspaces via nav (M1-29) render | [ ] | | |
| 7 | Findings page (step 10) | [ ] | | |
| 8 | Supervisor page (step 14) | [ ] | | |
| 9 | Assistant: cited answer + refusal (M1-01) | [ ] | | |

RULE: any [X] gets fixed OR its surface removed from the walk. Zero red banners in the room.
DO-NOT-TOUCH list (from the feature audit): Clone/Remove menu items, donut-segment clicks, drilldown, Add-widget entry.

=====================================================================
## PART 2 - M1-14: The rehearsal script (run twice; target <40 min)
=====================================================================

OPENING (30 sec): "PlantProcess IQ turns your existing plant databases into
process-to-quality intelligence - read-only, evidence-grade. Let me show you."

1. DISCLOSURE (verbatim): "I prepared and trained this instance on our emulated
   multi-source plant so we spend our time on the product - on your install this
   starts empty and fills through the DB-link, which I'll show you right now."
2. CONNECTIONS: show the connected sources incl. the two Oracle (PPIQ_SRC).
3. LIVE REGISTER (M1-28): meltshop v_parameter_definitions -> map -> execute ->
   show staging rows -> show canonical rows with source_system + batch id.
   "Those rows came through the read-only DB-link, tagged and traceable."
4. GENEALOGY: search a coil -> walk to heat -> click provenance.
5. WORKSPACES: open PRODUCTION_OVERVIEW. Drag a widget, resize, click a bar ->
   watch the workspace cross-filter. Switch a chart type. Save layout.
   "This is the no-code analytics surface - built, not mocked."
6. FINDINGS + ENGINE: open findings. Run one governed analysis live.
   IF completed: "here's a process-to-quality correlation with its q-value."
   IF blocked: "watch - the engine REFUSES to compute on insufficient data.
   That honest abstain is the moat: it will never fabricate a number it can't
   defend. No competitor shows you that."
7. SUPERVISOR (step 14): "the weekly supervisor re-tunes the jobs - v0 today,
   the closed loop is the pilot keystone."
8. ASSISTANT: ask "which source tables are registered?" -> cited answer.
   Ask "which coils will fail tomorrow?" -> refusal. "Same honesty."
9. CLOSE: "Working core today; the visual authoring canvas and the second-
   industry proof are the pilot. Everything you saw ran on real machinery."

CONTINGENCY (one line each - say, don't panic):
- Source down: "the connector reports it unreachable - the same honesty you get in production."
- API down: "let me restart the service - one moment." (have the start command ready)
- Widget dead: "that view is still being certified - here's the one next to it."
- Any red banner: "I'll note that and follow up" - move on, never debug live.

Run 1: ___ min, banners ___    Run 2: ___ min, banners ___

=====================================================================
## PART 3 - M1-15: Deck outline (evidence-linked; honesty-lint = no claim without a source)
=====================================================================

Slide 1  Title: PlantProcess IQ - process-to-quality intelligence for plants.
Slide 2  The problem: plant data siloed across Oracle/MySQL/SQL Server/historians.
Slide 3  Architecture: read-only DB-link -> staging -> canonical -> engine.
         SPEAKER NOTE: the disclosure sentence (verbatim, from Part 2 step 1).
Slide 4  Live demo marker (steps 1-5 of the walk).
Slide 5  The 15-step journey (screenshot of the green rail - M1-38).
Slide 6  Interactive workspaces (screenshots from the M1-11 certified pass ONLY).
Slide 7  The engine + honest-abstain moat.
         RULE: money number ONLY if a completed run id exists; else the moat framing.
Slide 8  Assistant: grounded answers + principled refusal (M1-01 screenshots).
Slide 9  VISION - "Pilot Milestone - Design" badge: ETL block canvas mockup.
Slide 10 VISION badge: AI/ML toolbox composer mockup.
Slide 11 VISION badge: statistics function palette mockup.
Slide 12 Roadmap: M2 (canvas, associative engine, catalogue, user/license, infra),
         M3 (second-industry proof). Honest, funded, sequenced.
Slide 13 Ask / next steps / pilot proposal.

HONESTY-LINT before you present: every claim slide names a batch id, run id,
screenshot, or is written in future tense. No 9.3x without a run id beside it.
The 3 vision slides MUST carry the visible Design badge.
