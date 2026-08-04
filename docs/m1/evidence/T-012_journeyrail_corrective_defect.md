# T-012 CORRECTIVE DEFECT - JourneyRail certification and the J15 route match

**Raised** 04-Aug-2026, during T-032 execution.
**Owning task** T-012 "Canonicalise the JourneyRail to J1 to J15", status Done.
**Under the standing status rule** a task whose evidence is invalidated becomes REOPENED. This record is the input to that corrective task.
**Not fixed in T-032.** Ruled 04-Aug: the failures are pre-existing, they are not T-032 regressions, and the corrective work does not belong in the T-032 pack.

---

## 1. What fails

`Frontend/PlantProcess.Web/src/components/journey/__tests__/JourneyRail.certification.test.tsx` - three tests, all three failing.

## 2. Proof that it is pre-existing

Reproduced twice with identical test names and identical messages:

| Run | Tree | JourneyRail result |
|---|---|---|
| 04-Aug 12:46 | pre-T-032. The T-032 v1 pack had already self-reverted, and the build output carried `VisualJoinCanvasPage-B1HKLBfY.js`, so no T-032 file was on disk | 3 of 3 failed |
| 04-Aug 13:04 | T-032 plus T-032a applied | 3 of 3 failed |

The three failures are therefore not T-032 regressions.

## 3. The three findings, separated by kind

### Finding A - STALE EXPECTATION, test side

Test: "renders all 15 canonical stages plus the operational alerting entry".
At `/data-integration/connections` the test expects `Step 1 of 15`. The rail renders `Step 4 of 15 - Declare read-only connections`.

T-012 renumbered the rail to Chapter 2 section 3.3.1, where J1 to J3 are the commissioning stages and connections is J4. The test was written against the pre-T-012 numbering and was not updated.

### Finding B - STALE EXPECTATION, test side

Test: "marks the current route as the current journey step".
At `/data-integration/supervisor` the test expects `Step 14 of 15` and a `Supervisor` link carrying `aria-current="step"`. The rail renders `Step 15 of 15 - Operate, govern and retain`.

Supervisor is no longer a stage of its own. It is a match prefix inside J15, so there is no `Supervisor` node to carry `aria-current`.

### Finding C - LIVE PRODUCT DEFECT, rail side

Test: "maps assistant configuration routes to the final assistant stage".
At `/assistant/configuration` the rail renders its idle heading `15-step product journey`, which is what it shows when NO stage matched.

The cause, measured:

| Where | What it says |
|---|---|
| `src/components/journey/JourneyRail.tsx` line 45 | J15's match list contains `/assistant-config` |
| `src/App.tsx` line 862 | `<Route path="/assistant-config" element={<Navigate to="/assistant/configuration" replace />} />` |
| `src/App.tsx` line 700 | `/assistant/configuration` is the real route |
| `src/components/AppLayout.tsx` line 79 | `/assistant/configuration` has its own left-navigation entry, "Assistant Configuration" |

The rail matches the REDIRECT SOURCE. The redirect fires immediately, the user lands on the canonical path, and the match is lost. A live, navigable surface therefore shows a journey rail with no current step.

This is a product defect, not a test defect. Findings A and B are corrected in the test; finding C is corrected in the rail.

## 4. Why the T-012 guard did not catch it

`src/test/architecture/journeyRailCanonical.test.ts` asserts that `STAGES` declares exactly fifteen entries, that every label is verbatim from Chapter 2 section 3.3.1, and that a forbidden set of routes is never a journey target. It says nothing about whether each MATCH PREFIX resolves to a live, non-redirect route. It passed on both runs and was correct to.

## 5. Required remedy

1. Update findings A and B to the canonical numbering, so the certification test asserts what T-012 actually built.
2. Repoint the J15 match from the redirect source `/assistant-config` to the canonical `/assistant/configuration`. Whether the redirect source is also kept is a judgement for the corrective task; matching only the source is what is wrong.
3. **Add the mechanical guard, ruled 04-Aug:** every match prefix in `STAGES` must resolve to a real `Route` element in `App.tsx` and never to a `Navigate`. This defect class is mechanical, so it gets a guard rather than a promise - a prefix pointing at a redirect must fail the build.

## 6. Audit the same class before closing

Finding C was found by accident, through an unrelated test. The corrective task should apply the new guard to ALL fifteen stages and to every prefix in every match list, not only to the one that surfaced.