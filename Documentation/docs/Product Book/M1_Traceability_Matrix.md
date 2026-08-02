# M1 DESIGN TRACEABILITY MATRIX

**Task:** T-001 | **Milestone:** M1 | **Phase:** M1-P1
**Save as:** `docs/m1/M1_Traceability_Matrix.md`
**Date:** 2 August 2026
**Status:** DRAFT - AWAITING SIGN-OFF. No other M1 task starts until this is signed.

**Sources.** Route list read from `Frontend/PlantProcess.Web/src/App.tsx` (69 declared route paths). Page contracts from Chapter 3 section 4.4 (A1 to F9 route pages, G1 to G6 shell components). Journey steps from Chapter 2 section 3.3.1 (J1 to J15). Behaviour sections from Chapter 4. Tutorials from Chapter 5 section 6.0.1 (T1 to T8, which walk J4 to J15 in full; J1 to J3 are commissioning prerequisites).

---

## 0. HOW TO READ AND SIGN THIS

**Classification, one per row:**

| Code | Meaning |
|---|---|
| **KEEP** | The visible surface already matches the final design. Verify, do not rebuild |
| **MODIFY** | The visible surface exists but needs change to match the final design |
| **TEMP-ADAPTER** | The visible surface is final; the persistence or algorithm behind it is temporary and M2 replaces it. **Permitted only when replacing it later changes nothing the customer can see** |
| **NEW** | The surface does not exist and must be built to the final design |

**The rule this matrix enforces.** Every screen opened in the room becomes a frozen contract. The customer must receive the same product after M2: same navigation, same control placement, same authoring flow, same terminology, same refusal semantics. **Showing a screen costs the right to redesign it.**

**Sign-off means three things:** the screen list is final, no screen outside it will be opened, and every row's classification is agreed.

---

## 1. THE PRESENTATION SCREEN LIST

Nineteen screens across the six beats. Ordered as they will be opened.

| # | Screen | Route | Beat | J step | Ch3 contract | Ch4 behaviour | Ch5 tutorial | Current implementation | Class |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Home | `/` | 3 | J1 | **A2** | 3.12 UX contract | 6.1 | **None.** `App.tsx:537` declares `<Route index>` redirecting to `/dashboard` | **NEW** |
| 2 | Connections | `/data-integration/connections` | 3 | J4 | **B1** | - | T1 | `ConnectionsRoute` under `DataIntegrationLayout`, `App.tsx:487` | MODIFY |
| 3 | Dataset Registry | `/data-integration/registry` | 3 | J5 | **B2** | - | T2 | `TableRegistryRoute`, `App.tsx:488` | MODIFY |
| 4 | Prepare Import | `/data-integration/prepare` | 3 | J5 | **B3** | - | T2 | `SourceImportPrepPage`, `App.tsx:497` | KEEP |
| 5 | Importing | `/data-integration/importing` | 3 | J6, J8 | **B4** | - | T2, T4 | `ImportingRoute`, `App.tsx:489` | MODIFY |
| 6 | Jobs Monitor | `/data-integration/jobs` | 3 | J6, J15 | **B5** | 5.3 | T7 | `JobsMonitorRoute`, `App.tsx:490` | MODIFY |
| 7 | Transformation Studio (Join Canvas) | `/prep/canvas` | **1**, 3 | J7 | **C1** | **5.2** whole section | T3 | `VisualJoinCanvasPage`, 784 lines, `App.tsx:505` | MODIFY |
| 8 | Relationship Browser | `/relationships` | 3 | J7 | **C6** | 3.15 | T3 | **None** | **NEW** |
| 9 | Mapping Health | `/mapping-health` | 3 | J8 | **C2** | - | T4 | `MappingHealthPage`, `App.tsx` route `/mapping-health` | MODIFY |
| 10 | Data Quality | `/data-quality` | 3 | J8 | **C3** | - | T4 | `DataQualityPage` | KEEP |
| 11 | Genealogy Explorer | `/materials`, `/materials/{id}` | 3 | J9 | **C5** | - | T4 | `MaterialInvestigationPage`, `App.tsx:549` and `:556` | MODIFY |
| 12 | Page Builder | `/page-builder` | **2** | J10 | **D2** | 5.1.9, 5.1.10 | T5 | `PageBuilderPage`, `App.tsx:170` | MODIFY |
| 13 | Interactive Workspace - six showcase pages | `/workspace/:dashboardCode` | **2**, 3 | J10, J11 | **D1** | **5.1** whole section | T5, T8 | `RoutedInteractiveWorkspacePage`, `App.tsx:484` | MODIFY |
| 14 | Analysis Toolbox | `/analysis/toolbox` | **1**, **4** | J12 | **D3** | 5.2, 5.4 | T6 | `AnalysisToolboxPage`, `App.tsx:513` | MODIFY |
| 15 | Analysis job configuration | `/investigate/analysis-jobs` | 3, **4** | J12, J15 | **F4** (partial) | 5.3.6, 5.4 | T7 | `AnalysisJobConfigPage`, `App.tsx:521` | **TEMP-ADAPTER** |
| 16 | Findings | `/correlations` | **4** | J12, J13 | **D4** | **5.5** | T8 | `CorrelationPage`, `App.tsx` route `/correlations` | MODIFY |
| 17 | Risk Dashboard | `/risk` | **4** | J13 | **D5** | 5.6 | T8 | `RiskDashboardPage` | **TEMP-ADAPTER** |
| 18 | ML Readiness and Models | `/ml-readiness` | **4** | J12 | **D8** | 5.4.3, 5.6.5 | T6 | `MlReadinessPage`, `App.tsx:183` | MODIFY |
| 19 | Website presentation path | `/`, `/product`, `/proof`, `/security`, `/contact` | **6** | - | Ch6 6.2 | - | - | 77 files, 20 components | MODIFY |

### Shell components - present on every screen above

| Screen | Ch3 contract | Ch4 behaviour | Current implementation | Class |
|---|---|---|---|---|
| Assistant dock | **G1** | **5.7.1** | `AssistantChat.tsx` rendered by **one page only**, `Phase8/AssistantRuntimePage.tsx` | **MODIFY - visible contract is wrong today** |
| Application header and navigation | **G2** | 3.12 | `AppLayout.tsx`, five `NavGroup` groups plus Workspaces | MODIFY |
| Notification and toast host | **G4** | 3.12 | `useStandardToast` | KEEP |
| Refusal and error boundary | **G5** | 3.12 | `withPageBoundary`, `DataFetchBoundary`, `ErrorBoundary` | KEEP |
| Activity and progress tray | **G6** | 3.12 | **None** | **NEW** |
| Global search and command palette | **G3** | 3.12 | **None** | **NOT SHOWN** - out of the beat set, deferred to M2 |

---

## 2. VALIDATION: ZERO BLANK CHAPTER 3 CELLS

The task's acceptance criterion is that no row has a blank Chapter 3 column. **All nineteen screens and all six shell rows map to a Chapter 3 contract.** One row needs its exception recorded:

- **Row 15, Analysis job configuration.** Chapter 3 has no page contract for a standalone analysis-job configuration page; the closest is **F4 Jobs Administration**, which is an admin surface. The existing `AnalysisJobConfigPage` predates the design. **Ruling required at sign-off:** either fold this screen into D3 Analysis Toolbox for the presentation, or accept F4 as its contract and align its terminology to F4. It cannot stay unmapped, because an unmapped screen shown in the room is an unowned frozen contract.

---

## 3. THE FOUR NEW SURFACES, AND WHAT THEY COST

Four items are classified NEW. Three of them are already funded in the M1 backlog; one is not, and needs a decision.

| Item | Backlog cover | Note |
|---|---|---|
| **A2 Home** | **NOT FUNDED** | The root currently redirects straight to `/dashboard`. Chapter 3 A2 specifies a Home page and J1 lands on it. **Decision at sign-off:** build a minimal A2 Home carrying the readiness panel (which T-041 builds anyway and must appear on Home per its own text), or narrate the redirect and never open `/`. Building it is roughly 6 hours and it is the natural home for the readiness authority |
| **C6 Relationship Browser** | Partly, via T-036 and T-037 | Those tasks build the relationship model slice and its resolver. The browser page itself needs a minimal read-only view for the room |
| **G1 Assistant dock** | Yes - T-046 | 8 hours, M1-P5 |
| **G6 Activity and progress tray** | Yes - T-035 | Import progress visibility, 4 hours, M1-P4 |

---

## 4. ROUTES THAT MUST NOT BE OPENED IN THE ROOM

`App.tsx` declares 69 route paths. Nineteen screens are in the presentation. The rest fall into three groups, and each group has a different instruction.

### 4.1 Legacy phase-token redirects - nine routes

`/phase8/assistant`, `/phase8/assistant-config`, `/phase8/suggestions`, `/phase9/access`, `/phase9/executive`, `/phase15/benchmarking`, `/phase15/honesty-certification`, `/phase15/recommendations`, `/phase15/roi-cfo-dashboard`, `/phase15/scenario-simulation`, `/phase15/value-realization`.

**Verified fact, and it corrects the task description as written:** every one of these is a `<Navigate>` reverse redirect created by the M1-08 canonical rename, not a live page. **And none of them is reachable from any navigation group** - the audit in T-002 confirms every `to:` value in `AppLayout.tsx` is already canonical.

**Instruction:** leave them in place for M1 (they are Rule 4 retirement debt, not demo risk), record them on the M2 retirement list, and add the ratchet that stops a new one appearing. T-002 ships the ratchet.

### 4.2 Other canonical redirects - six routes

`/material-investigation`, `/material-investigation/:materialUnitId`, `/quality`, `/correlation`, `/edge-agent`, `/connectors/historian`, `/commercial-license`, `/assistant-config`, `/investigate/inspect`. Same treatment: keep, retire in M2.

### 4.3 Real pages outside the beat set - not opened

`/analytics-widgets`, `/widget-script-compiler`, `/dashboard/widgets/schema-drift`, `/brand`, `/admin-preview`, `/admin/*`, `/executive`, `/access-matrix`, `/i18n-rtl`, `/value/executive`, `/value/scenario`, `/advisory/*` (six routes), `/suggestions`, `/assistant`, `/assistant/configuration`, `/edge-collector`, `/historian-connector`, `/pages/:slug`, `/data-integration/alerting`, `/data-integration/supervisor`, `/data-integration/author-mapping`, `/data-integration/connector-truth`.

**These are not defects.** Several are good work. They are simply outside the six beats, and **opening one of them freezes a contract nobody planned to freeze.** That is the whole point of this matrix.

**Two of them deserve a deliberate decision rather than a default:**

- **`/data-integration/author-mapping`** ("Load to Plant Data"). J8 projection is in beat 3 and the tutorial T4 walks it. If the demonstration shows projection, this screen is in the room and needs a matrix row. **Decide at sign-off.**
- **`/dashboard/widgets/schema-drift`** ("Widget Drift"). This is an internal diagnostic surface sitting in the Analytics navigation group, one click from the Command Dashboard. **Recommend hiding it from navigation for M1**, because a customer who clicks it sees engineering diagnostics on a screen that is not in the story.

---

## 5. FINDINGS RAISED WHILE BUILDING THIS MATRIX

Writing the traceability matrix found four things that reading the code alone did not.

1. **There is no Home page.** Chapter 3 A2 specifies one and J1 lands on it. The application redirects the root to `/dashboard`. Either build A2 or never open `/`.
2. **The assistant's visible contract is wrong today, not merely incomplete.** Chapter 4 5.7.1 requires a dock present on every authenticated page. A separate `/assistant` route is a different product shape. Shipping the route in M1 and the dock after M2 fails the Customer Contract Continuity Test on its own.
3. **Analysis job configuration has no Chapter 3 owner.** See section 2.
4. **Two customer-visible navigation strings carry internal engineering tokens** - `AppLayout.tsx:45` reads `"Weekly engine review (step 14)"` and `:90` reads `"Phase 15 advisory projection"`. Under the severity doctrine those are Severity 1: a customer reading a phase number on screen learns that the product is organised around our sprint plan. T-002 fixes both.

---

## 6. SIGN-OFF

| Question | Answer | Signed |
|---|---|---|
| Is the nineteen-screen list final? | | |
| Will any screen outside it be opened? | Must be **No** | |
| Ruling on row 15 - fold into D3, or adopt F4? | | |
| Build A2 Home, or never open `/`? | | |
| Is `/data-integration/author-mapping` in the room? | | |
| Hide `/dashboard/widgets/schema-drift` from navigation? | | |
| Are all classifications agreed? | | |

**Nothing else in M1 starts until every row above is answered.** That is the acceptance criterion of T-001 and it is not a formality: every unanswered row is a contract that gets frozen by accident.
