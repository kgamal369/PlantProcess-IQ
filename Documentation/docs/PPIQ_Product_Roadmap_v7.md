# PlantProcess IQ — Product Roadmap v7
**Issued:** 05-Jul-2026 · **Supersedes:** Roadmap v6 · **Companion:** Backlog v14 (140 tasks, milestone-driven), Status Review & Gap Analysis 05-Jul, Scoreboard 05-Jul (headline 52 / A4)
**Structure change vs v6:** the plan is now organized around four hard milestones with dated exit criteria, replacing the rolling 8-Jul/14-Jul/post-deal buckets. Every backlog task carries a Milestone (M1–M4) and, inside M1, a Ring (R1 must / R2 should / R3 docs-parallel).

---

## M1 — THE DEMO · Thursday 08-Jul (CEO + customer technical engineer)

**Mission:** the whole user journey — frontend and backend — works on the localhost laptop, starting from an **empty PlantProcess database exactly like a customer's day one**, filled live by running jobs against the emulated plant sources. Run it twice: a full dress rehearsal on 07-Jul evening, and the real thing live in the room. The customer must leave impressed and wanting to buy.

### The choreography (both rehearsals, identical)
1. `tools\reset-app-database.ps1` → ppiq_app is empty: 0 material units, 0 registrations, 0 jobs. Login as sysadmin. Every page renders an honest empty state — no crash, no fake data. *(V1-47)*
2. **DB-Link page**: connect the meltshop source, test the connection live. *(V1-18)*
3. **No-code configuration**: register two source tables through the picker; the mapper loads the staging schema, previews, joins. *(V1-20, P4 registry-depth)*
4. **Jobs page**: run Stage-1 → watch Jobs Monitor **and the new log tab** stream real job events; watermark proof on a second run. *(V1-19, V1-45/46)*
5. Stage-2 canonical refresh → **our database is now filled with customer data**. *(V1-21)*
6. **No-code dashboards**: bind widgets to the fresh canonical set; refresh shows live numbers. *(V1-22)*
7. **Genealogy walk** C-0044170 both directions; blended 70/30 attribution. *(V1-10/11)*
8. **Data analysis & correlation**: run an inspection, read ranked suspected contributors with population, method, q-value, honesty bar. *(V1-23/42)*
9. **AI**: grounded assistant answers one question over the results with citations — or the honest framing, decided at the 07-Jul-noon gate. **Prediction shown honestly** = the shipped Suggestion Engine + ranked contributors; we never fabricate a model. *(V1-43, V1-50)*
10. Live `UPDATE sites SET site_name='<CustomerName>'` → sidebar renames. Quiet proof the product is generic. *(V1-38)*

### Exit criteria (all hard)
- Journey-proof.md **0 FAIL** from the prover run as a file; the 8 MANUAL rows walked and initialed.
- Rehearsal #1 (07-Jul evening) completes ≤25 min with zero dead ends, recorded.
- **Basic logging live**: `systemlog_yyyyMMddHH.log` + `joblog_yyyyMMddHH.log` rolling hourly; job_log table filling from imports; log panel with type/severity/day filters at least on the demo path. *(V1-44/45/46-min)*
- 347 zombie runs gone; Jobs Monitor shows honest states; no run can hang past its max-runtime again. *(V1-41)*
- ≥1 correlation run completes to rows in `ml_correlation_results_v2` rendered in the HMI. *(V1-42 — currently the single blocking risk)*
- UI charm pass on the nine demo pages: consistent, professional, zero console warnings. *(V1-49, R2)*
- **Build freeze 08-Jul 09:00.** Nothing merges after freeze.

### Budget & rings (3 working days remain)
R1 **46.5 h** of code-critical work (fits 3 hard days at sprint pace, no slack for new scope) · R2 21 h (pull in only after R1 lands) · R3 9 h of deck/script fills (Karim-parallel, non-code). **Cut-line rule:** if 06-Jul ends without V1-42 green, J7 flips to the honest-framing script and its hours flow to logging + rehearsal polish. The demo is never cancelled; its shape adapts.

### Dependency spine
V1-42 root-cause → V1-41 reaper → prover J7 block → V1-50 showcase → V1-48 rehearsal. Logging (V1-44→45→46) is parallel and independent. V1-47 is prerequisite to any rehearsal.

---

## M2 — THE PRODUCT · by 23-Jul

**Mission:** what the customer saw becomes what the customer can buy and run.

1. **Server & delivery permanent** *(V2-37)*: Caddy routing fixed at source (no runtime alias), compose bind-mount resolved, CI/CD deploy gate blocking with post-deploy smoke, reboot-safe on Hetzner. Two-stack topology (infra vs app) documented and preserved.
2. **Logging 100%** *(V2-38)*: panel polish (typeahead, saved filters, CSV export), retention/purge proven, ops runbook, audit_log/job_log responsibilities settled in the Doctrine.
3. **Chatbot/LLM + prediction 100%** *(V2-36)*: provider CRUD on the real config schema, self-hosted + zero-retention cloud options, egress guard proven by capture, 25-question grounded eval green in CI.
4. **Hardening sweep**: every UI/no-code surface, dashboards, widgets, heat map — fault-injected, boundary-tested, dead-button-scanned. Rename wave clears phase codes from customer-inspectable code (`/admin/p03p04`, Phase9/10/15 dirs, obsolete CorrelationService deleted).
5. **Every displayed value correct and justified** *(V2-34)*: known-answer correctness harness in CI; a written map from every HMI figure to its engine field; prover numeric-audit block.
6. **License + roles >75%** *(V2-35)*: remote issue/renew/revoke with heartbeat + grace, in-app license admin, RBAC matrix enforced on every admin endpoint with per-role integration tests.

**Exit:** a clean `git push` deploys to Hetzner green; the correctness harness gates CI; revoking a license locks the app within one heartbeat; scorecard A4 ≥ 70.

---

## M3 — SCALE-READY · by 31-Aug

1. **Bug-burn to zero known Criticals** from M2 + evaluation feedback; every fix ships with a regression test *(V3-25)*.
2. **Multi-industry proof** *(V3-24)*: automotive and food/beverage emulated fleets ingest through the identical journey with **zero app changes** — the on-the-record evidence behind "sell the same binary to 20 industries." Seeds carry constructed known drivers so the correctness harness validates them too.
3. **Engine/ML/AI hardening** *(V3-26)*: measured performance envelopes (1M-row canonical), drift detection firing on injected drift, chatbot eval grown to 100 questions including adversarial uncited-number probes.
4. **License + roles 100%** *(V3-27)*: SSO/SCIM happy path, delegated tenant admin, and a commissioning runbook a non-author can execute end-to-end (Admin Golden Rule codified).

**Exit:** three industries demonstrably ingested by configuration alone; defect register clean; commissioning executed from the runbook by someone other than its author.

---

## M4 — THE CUSTOMER · contract-shaped

Held deliberately unshaped: the 25 V4 backlog tasks (SSO variants, connector breadth, advanced exports, tenant scale-out) are sequenced by the signed customer's sources, KPIs, and rollout plan. First activity on signature: a scoping workshop that maps their plant onto the generic journey and promotes the relevant M4 tasks into a dated plan.

---

## Standing rules carried into v7
- **Golden rule** (now true in code): no demo machinery in the product, ever; demo = external emulated fleets only.
- **Honesty over spectacle:** nothing is demoed that is not proven — the grounded assistant and J7 both carry wire-or-frame gates rather than fake fallbacks.
- **Headline scoring:** the lowest persona is the product score; current 52 (A4) with a named path to 70+ inside M1+M2.
- **Freeze discipline:** every milestone ends with a build freeze and an evidence pack (prover output, gate logs, walk script initialed).
