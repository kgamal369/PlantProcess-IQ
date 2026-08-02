# M1 DESIGN TRACEABILITY MATRIX - v1.1

**Task:** T-001 | **Milestone:** M1 | **Phase:** M1-P1
**Save as:** `Documentation/docs/Product Book/M1_Traceability_Matrix.md`
**Supersedes:** v1.0 (DRAFT) of the same date
**Status:** three of the six open questions are now CLOSED by decisions already taken in Backlog v2.2. Three remain for sign-off.

**Sources.** Routes from `Frontend/PlantProcess.Web/src/App.tsx` (69 declared paths). Page contracts from Chapter 3 4.4. Journey from Chapter 2 3.3.1. Behaviour from Chapter 4. Tutorials from Chapter 5 6.0.1.

**Reference convention changed in v1.1.** This document names tasks by NAME, never by id. Ids move when the backlog is reordered, and three stale references in v1.0 are exactly why.

---

## 0. WHAT CHANGED FROM v1.0

| # | v1.0 said | v1.1 |
|---|---|---|
| 1 | Row 15 `AnalysisJobConfigPage` was a presentation screen, classified TEMP-ADAPTER, with a ruling needed on whether to fold it into D3 or adopt F4 | **ROW REMOVED.** No ruling is outstanding: the backlog task *J12 Analysis authoring: converge onto D3 Analysis Toolbox in S3 mode* already decides it. Analysis authoring is D3 in S3 mode, and `AnalysisJobConfigPage` leaves the M1 navigation |
| 2 | Row 1 `A2 Home` was NEW and NOT FUNDED, with a decision needed on whether to build it | **ROW REMOVED, and this closes the question without spending six hours.** J1 to J3 are commissioning and are narrated, not demonstrated. The screen J1 lands on is therefore never opened. A2 Home is built in M2a-P4 with the rest of commissioning |
| 3 | Rows referenced backlog tasks by id | All references are by task name |
| 4 | Six sign-off questions | Three, listed in section 6 |

---

## 1. THE PRESENTATION SCREEN LIST

Seventeen screens. Ordered as they will be opened.

| # | Screen | Route | Beat | J step | Ch3 | Ch4 | Ch5 | Current implementation | Class |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Connections | `/data-integration/connections` | 3 | J4 | **B1** | - | T1 | `ConnectionsRoute` under `DataIntegrationLayout` | MODIFY |
| 2 | Dataset Registry | `/data-integration/registry` | 3 | J5 | **B2** | - | T2 | `TableRegistryRoute` | MODIFY |
| 3 | Prepare Import | `/data-integration/prepare` | 3 | J5 | **B3** | - | T2 | `SourceImportPrepPage` | KEEP |
| 4 | Importing | `/data-integration/importing` | 3 | J6, J8 | **B4** | - | T2, T4 | `ImportingRoute` | MODIFY |
| 5 | Jobs Monitor | `/data-integration/jobs` | 3 | J6, J15 | **B5** | 5.3 | T7 | `JobsMonitorRoute` | MODIFY |
| 6 | Transformation Studio | `/prep/canvas` | **1**, 3 | J7 | **C1** | **5.2** | T3 | `VisualJoinCanvasPage`, 784 lines - **converged into SharedAuthoringShell as the S1 face** | MODIFY |
| 7 | Relationship Browser | `/relationships` | 3 | J7 | **C6** | 3.15 | T3 | **None** | **NEW - decision open, see 6.2** |
| 8 | Mapping Health | `/mapping-health` | 3 | J8 | **C2** | - | T4 | `MappingHealthPage` - summary only, no issue-row model | MODIFY |
| 9 | Data Quality | `/data-quality` | 3 | J8 | **C3** | - | T4 | `DataQualityPage` | KEEP |
| 10 | Genealogy Explorer | `/materials`, `/materials/{id}` | 3 | J9 | **C5** | - | T4 | `MaterialInvestigationPage` - legacy workbench, converging to the two-state contract | MODIFY |
| 11 | Page Builder | `/page-builder` | **2** | J10 | **D2** | 5.1.9, 5.1.10 | T5 | `PageBuilderPage` - hardcoded widget library, reducer knows five kinds | MODIFY |
| 12 | Interactive Workspace, six showcase pages | `/workspace/:dashboardCode` | **2**, 3 | J10, J11 | **D1** | **5.1** | T5, T8 | `RoutedInteractiveWorkspacePage` | MODIFY |
| 13 | Analysis Toolbox | `/analysis/toolbox` | **1**, **4** | J12 | **D3** | 5.2, 5.4 | T6 | `AnalysisToolboxPage` - **becomes the S3 face of the shared shell and the sole analysis authoring surface** | MODIFY |
| 14 | Findings | `/correlations` | **4** | J12, J13 | **D4** | **5.5** | T8 | `CorrelationPage` | MODIFY |
| 15 | Risk Dashboard | `/risk` | **4** | J13 | **D5** | 5.6 | T8 | `RiskDashboardPage` | **TEMP-ADAPTER** |
| 16 | ML Readiness and Models | `/ml-readiness` | **4** | J12 | **D8** | 5.4.3, 5.6.5 | T6 | `MlReadinessPage` | MODIFY |
| 17 | Website presentation path | `/`, `/products`, `/product`, `/proof`, `/security`, `/contact` | **6** | - | Ch6 6.2 | - | - | 77 files, 20 components | **MODIFY - five-product IA corrected in M1** |

### Shell components, present on every screen above

| Screen | Ch3 | Ch4 | Current implementation | Class |
|---|---|---|---|---|
| Assistant dock | **G1** | **5.7.1** | `AssistantChat.tsx` rendered by one page only, `Phase8/AssistantRuntimePage.tsx` | **MODIFY - the visible contract is wrong today** |
| Header and navigation | **G2** | 3.12 | `AppLayout.tsx`, five `NavGroup` groups plus Workspaces | MODIFY |
| Notification and toast host | **G4** | 3.12 | `useStandardToast` | KEEP |
| Refusal and error boundary | **G5** | 3.12 | `withPageBoundary`, `DataFetchBoundary`, `ErrorBoundary` | KEEP |
| Activity and progress tray | **G6** | 3.12 | **None** - built by the import progress task | **NEW** |
| Global search and command palette | **G3** | 3.12 | **None** | **NOT SHOWN** - outside the beat set, M2a |

---

## 2. VALIDATION: ZERO BLANK CHAPTER 3 CELLS

All seventeen screens and all six shell rows map to a Chapter 3 contract. **The one exception recorded in v1.0 is gone**, because the screen that had no owner is no longer a presentation screen.

---

## 3. ROUTES THAT MUST NOT BE OPENED

### 3.1 Legacy phase-token redirects

`/phase8/assistant`, `/phase8/assistant-config`, `/phase8/suggestions`, `/phase9/access`, `/phase9/executive`, `/phase15/benchmarking`, `/phase15/honesty-certification`, `/phase15/recommendations`, `/phase15/roi-cfo-dashboard`, `/phase15/scenario-simulation`, `/phase15/value-realization`.

Every one is a `<Navigate>` reverse redirect from the M1-08 canonical rename. **None is reachable from any navigation group** - verified, and now enforced by PPIQ-T12 `navigationContract.test.ts`. Leave them; retire in M2b.

### 3.2 Other canonical redirects

`/material-investigation`, `/material-investigation/:materialUnitId`, `/quality`, `/correlation`, `/edge-agent`, `/connectors/historian`, `/commercial-license`, `/assistant-config`, `/investigate/inspect`. Same treatment.

### 3.3 Real pages outside the beat set

`/analytics-widgets`, `/widget-script-compiler`, `/dashboard/widgets/schema-drift`, `/brand`, `/admin-preview`, `/admin/*`, `/executive`, `/access-matrix`, `/i18n-rtl`, `/value/executive`, `/value/scenario`, `/advisory/*`, `/suggestions`, `/assistant`, `/assistant/configuration`, `/edge-collector`, `/historian-connector`, `/pages/:slug`, `/data-integration/alerting`, `/data-integration/supervisor`, `/data-integration/connector-truth`, **and `/investigate/analysis-jobs`**.

These are not defects. Several are good work. Opening one freezes a contract nobody planned to freeze.

**Two still need a deliberate decision:**

- **`/data-integration/author-mapping`** ("Load to Plant Data"). J8 projection is in beat 3 and tutorial T4 walks it. If the demonstration shows projection, this screen is in the room and needs a matrix row.
- **`/dashboard/widgets/schema-drift`** ("Widget Drift"). An internal diagnostic surface sitting in the Analytics navigation group, one click from the Command Dashboard. **Recommend hiding it from navigation for M1.**

---

## 4. THE THREE NEW SURFACES AND THEIR COVER

| Item | Backlog cover | Note |
|---|---|---|
| **C6 Relationship Browser** | Backend slice only | The relationship model tasks build the tables and the resolver. **The page itself is not funded.** See 6.2 |
| **G1 Assistant dock** | Yes - *Build the G1 persistent assistant dock* | 8 h, M1-P5 |
| **G6 Activity and progress tray** | Yes - *J6 Import progress visibility* | 4 h, M1-P4 |

---

## 5. FINDINGS THE MATRIX PRODUCED

1. **The assistant's visible contract is wrong today, not merely incomplete.** Chapter 4 5.7.1 requires a dock on every authenticated page. A separate route is a different product shape, and shipping the route now and the dock after M2a fails the Continuity Test on its own.
2. **Two customer-visible navigation strings carried internal engineering tokens.** Fixed and now ratcheted by PPIQ-T12.
3. **The Page Builder widget library is hardcoded and its reducer knows five widget kinds.** That is not the final D2 surface, and it carries half of customer beat 2.
4. **The website removes direct product navigation and a validator asserts the removal** - the opposite of Chapter 6 6.2, which states SOU has five separate products with PPIQ as flagship and not as a container. Corrected in M1, not deferred.

---

## 6. SIGN-OFF - THREE QUESTIONS REMAIN

### 6.1 Is the seventeen-screen list final, and will nothing outside it be opened?

| | Answer | Signed |
|---|---|---|
| List final | | |
| Nothing outside it opened | Must be **Yes** | |

### 6.2 C6 Relationship Browser - minimal page, or not shown?

The relationship model slice is funded and it is one of the strongest differentiators in the product: a join declared once, published, versioned, and resolved by every consumer. **But nothing renders it.** Two options:

- **Show it.** Add a minimal read-only page listing published relationships, their members and their paths. Roughly 4 hours, and it belongs in M1-P4 beside the resolver.
- **Do not show it.** The relationship still does its work invisibly behind the cross-source widget, and you narrate it. Zero hours, and the customer never sees the mechanism that separates this product from a dashboard with joins.

**Recommendation: show it.** Four hours to make the differentiator visible is the best-value decision on this page.

### 6.3 Two route decisions

| Question | Answer | Signed |
|---|---|---|
| Is `/data-integration/author-mapping` in the room? | | |
| Hide `/dashboard/widgets/schema-drift` from navigation? | | |

---

**Nothing else in M1 starts until section 6 is answered.** Every unanswered row is a contract that gets frozen by accident.
