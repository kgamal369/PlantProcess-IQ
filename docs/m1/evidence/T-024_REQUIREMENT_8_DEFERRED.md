# T-024 REQUIREMENT 8 - DEFERRED

Task     : T-024 - Replace the presentation operational population with the Fleet v2 emission
Recorded : 2026-08-05

--------------------------------------------------------------------------------
STATUS
--------------------------------------------------------------------------------

  Requirements 1 to 7   PASS
  Requirement 8         DEFERRED - final browser acceptance pending completion
                        and convergence of the M1 customer-visible presentation
                        surfaces.

  DEFERRED is not PASS, is not FAIL, and is not waived.
  T-024 therefore does NOT hold a Done status.

--------------------------------------------------------------------------------
WHY
--------------------------------------------------------------------------------

Several of the surfaces requirement 8 is meant to certify are still under active
implementation in later M1 frontend, dashboard and chart tasks - dashboards,
charts, analytical widgets, authoring surfaces, page bindings and final
presentation states.

Running the final browser acceptance against them now would create false closure
work: T-024 would begin fixing unfinished UI that the backlog already assigns to
later tasks. That is scope absorption, and avoiding it is the point of the
deferral.

The browser walk CERTIFIES what the backlog has produced. It is not the vehicle
for building those surfaces.

--------------------------------------------------------------------------------
EXPLICITLY NOT TO BE DONE FROM T-024
--------------------------------------------------------------------------------

No work is to be opened on the following merely because the eventual walk will
inspect them. If a page, chart or widget is owned by an existing backlog task,
that task implements it.

  /dashboard
  /analytics-widgets
  /correlations
  /correlation
  /risk
  material-investigation presentation

--------------------------------------------------------------------------------
WHAT THE WALK WILL BE, WHEN IT IS RUN
--------------------------------------------------------------------------------

Once the relevant M1 frontend, dashboard and chart tasks have reached their final
customer-visible state, the seven checks are run ONCE against the final surfaces:

  A. /dashboard renders normally, no permanent spinner or broken panel, the
     production and shift presentation is populated.
  B. Defect Pareto carries Fleet v2 defect vocabulary only, SCALE near the
     expected top share of about 26 percent, LAMINATION and SENSOR_ARTEFACT near
     the low end at about 2 percent, and Disposition is NOT presented as a defect
     class.
  C. /materials shows Heat, Slab and Coil, and one real current coil opens.
  D. /material-investigation/{coil} genealogy reaches back to its heat with no
     broken genealogy state.
  E. Equipment and downtime presentation shows Fleet equipment only, with stopped
     duration and production-impact duration as separate quantities and no reused
     or mislabelled single value.
  F. Parameter trend and by-grade surfaces render current Fleet v2 data with no
     Pharma, Tire or Aluminum parameter vocabulary.
  G. Analysis truth, and the two engines are NOT in the same state:
       Correlation - current findings are 0, no quarantined July result is
         resurrected, the exclusion or refusal state is presented honestly, and
         an exclusion row is never rendered as a correlation finding.
       Risk - the current 500-score population may render; it must not claim
         exhaustive coverage of all 35,910 eligible material units.

  Scope note: inspect the selectors and surfaces actually customer-visible in the
  presentation path. Searching every dormant endpoint or unused selector
  implementation would reopen requirement 7 rather than perform requirement 8.

  /ml-readiness is useful supporting evidence and may be checked, but it is not
  an additional blocker if the required surfaces above already prove the state
  honestly.

--------------------------------------------------------------------------------
CLOSURE PATH
--------------------------------------------------------------------------------

  all seven pass       -> Requirement 8 PASS -> T-024 DONE
  one check fails      -> fix that specific presentation defect only, repeat only
                          the affected check, then close T-024

No broad re-audit. T-025 is closed and is not reopened. No new database
investigation is opened by this walk.