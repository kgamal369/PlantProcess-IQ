# M1 Dress-Rehearsal Protocol (V1-48) - run twice: 07-Jul evening (recorded) + live

## Pre-flight (T-15 min)
- [ ] Source fleet up: docker ps shows the meltshop container(s) running.
- [ ] API running (start-api.ps1 -Profile local); startup log shows systemlog_ path + "Stuck-run reaper active".
- [ ] Web running (start-web.ps1 -Profile local); loads on http://localhost:5173.
- [ ] Fresh DB: tools\reset-app-database.ps1 -> type RESET -> day-one counts all 0.
- [ ] Second terminal ready with the walk prover for the live evidence pass.

## The walk (<= 25 min, every click from the 9-step script)
1. J1  Cold-load the app -> lands authenticated as sysadmin, nav visible, no login page, clean console.
2. J2  DB Configuration -> connect the meltshop source -> Test -> green. Register TWO tables.
3. J3  Importing Data -> run Stage-1 -> Jobs Monitor shows status/rows/duration -> open the
        LOG TAB at the bottom -> Import-Stage1 Started/Completed events streaming.
4. J4  Schema mapper -> load staging schema -> preview -> join across the two tables.
5. J5  Run Stage-2 canonical refresh -> monitor + log tab show it. "Our database is now filled
        with the customer's data."
6. J6  Dashboards -> create a page, drag a widget, bind to a canonical view, save. Re-run import
        -> refresh -> the number visibly changes (seam-6: reads canonical, not seed).
7. J7  Advanced Analysis -> configure an inspection (defect + window) -> run -> ranked suspected
        contributors with population + method + q-value + AnalysisHonestyBar. Superheat driver on top.
8. AI  Grounded assistant explains a finding with citations (or the V1-43 framing verbatim).
9. Close: live UPDATE sites SET site_name = '<CustomerName>'; reload -> sidebar renames.
        "Same product, your plant's name, your data - nothing hardcoded."

## Evidence pass (parallel, second terminal)
- [ ] Invoke-PpiqJourneyWalk.ps1 -> walk-evidence.md attached, 0 FAIL on the automated rows.

## Abort/adapt rules
- If J7 live inspection is not green: show the 13-finding result set that IS proven, use J7 framing.
- If the assistant provider is not wired: V1-43 framing verbatim; never fake an answer.
- Timebox: if any step exceeds 4 min, narrate and move on; the story is end-to-end flow, not depth.
