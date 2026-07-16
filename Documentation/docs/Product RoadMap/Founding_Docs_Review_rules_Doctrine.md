# Founding-Documents Review - rules.txt & Doctrine v8
**15-Jul-2026, 15:25** | Companion to: concept Amendment Sheet v1.1, Roadmap v9
Evidence base: live-source probes + sweeps of 15-Jul; concept.md v1.0/v1.1; Roadmap v9.

---

## PART 1 - rules.txt (founding document, 631 lines)

### 1.0 File-integrity finding
rules.txt is a COMPOSITE: lines 1-190 are the six founding rules; line 191 onward is a
concatenated second document ("PART A - Six Evaluation Personas / PART B - Capability
Specification" - the Aspects-of-Review lineage). Recommendation: split the file. The
founding rules are a historical artifact to preserve verbatim (like the audits); the
appended spec belongs with Aspects of Review v4, not here. Note also the internal header
says "4 Rules" while six exist - evidence the file grew without version discipline.

### 1.1 Absorption audit - where each founding rule landed

| rules.txt | Destiny in concept v1.1 | Verdict |
|---|---|---|
| Rule 1 Product Definition | Vision (section 1) | ABSORBED |
| Rule 2 Generic | Rule 1 GENERIC ONLY | ABSORBED, sharpened |
| Rule 3.1-3.2 Starts empty, DB-link only door | Rule 2 | ABSORBED, sharpened (taxonomy clarification added 12-Jul) |
| **Rule 3.3 ONE PostgreSQL DB, TWO SCHEMAS (Meta Data / Plant Data)** | **DROPPED** | **NEEDS RATIFICATION - see 1.2.A** |
| Rule 4 First-day journey | Rule 3 + the 15 steps | ABSORBED (grown from the original list) |
| Rule 5.1 Users/Roles | section 6 Users, roles, licensing | ABSORBED |
| Rule 5.3 Job monitoring | Journey job/monitor doctrine | ABSORBED |
| Rule 5.4 Logs: 3 types x 3 severities, log tab on every page | section 6 Logging names FOUR layers (request/audit/job/assistant); "data logs" became UI-4 plant_data_log | ABSORBED WITH TAXONOMY CHANGE - ratify the 4-layer model as the successor |
| **Rule 5.5 FOUR license tiers (Lite/Pro/Pro Plus/Enterprise) + prices (15/25/40/50k initial; 2.5/3.5/4.5/5.5k monthly)** | concept names THREE tiers (Standard/Pro Plus/Enterprise); current commercial doctrine prices differ again | **CONFLICT - see 1.2.B** |
| **Rule 5.2 License-toggle demo moment** (switch tiers live, features appear/disappear) | Absent from concept; partially in Doctrine section 9 | **NEEDS DECISION - see 1.2.C** |
| Rule 6 The 4 low-code UIs | Journey UI-1..UI-4 | ABSORBED |

### 1.2 The three items needing your explicit ruling (constitutional change control)

**A. The two-schema mandate (Rule 3.3).** The founding rule requires one PostgreSQL
database with a physical METADATA schema (pages, dashboards, users, jobs - allowed
populated day one) and a physical PLANT DATA schema (empty day one). The build
implements this LOGICALLY (config-class vs plant-data tables) but not as two named
physical schemas. Options: (1) ratify the logical interpretation into concept v1.1
as Amendment 6 - cheapest, honest; (2) mandate the physical split as an M2 task -
real migration work, cleaner Rule-2 story for A2 reviewers ("SELECT count(*) FROM
plant_data.* = 0 on day one" is a one-line proof). Recommendation: (1) now, (2) as
an M3 consideration.

**B. Tier/pricing canon.** Three documents disagree: rules.txt (4 tiers, 15/25/40/50k),
concept section 6 (3 tiers: Standard/Pro Plus/Enterprise), current commercial doctrine
(12/28/50k deposits + monthly). The frontend still carries the OLDEST scheme: the
licenseTier type union reads "Light" | "Pro" | "Pro Plus" | "Enterprise" | "Demo"
(AppCommandHeader/AppDualBrandHeader) - four legacy names plus a Demo tier that
violates Rule 1 at the type level. Ruling needed on the canonical tier set; then one
M2 task aligns concept section 6, Doctrine section 9, the frontend unions, and the
tier->feature matrix to it.

**C. The license-toggle demo moment (Rule 5.2).** A founding requirement with real
selling power (feature disappears live when downgraded) that the constitution never
codified. If ratified, it is an M2/M3 demo-script item riding the existing
entitlement gates; if retired, record the retirement so it stops haunting reviews.

---

## PART 2 - Doctrine v8 (3 Jun 2026, delta 26 Jun; 23 sections + 9 appendices)

### 2.1 Verdict
The strongest specification document in the set - the buyer story, honesty contract,
worked examples, objection playbook, and acceptance-gate structure are excellent and
largely timeless. But it is FIVE WEEKS STALE, predates the constitution (supremacy
inversion: concept.md requires every document to derive from it; the master spec
cannot cite a document younger than itself), and its realization chapters describe a
plan (Waves A-D) that the M1-M4 roadmap superseded. Verdict: **amend to v8.1 - keep
Part I (specification), amputate Part II (realization) in favor of Roadmap v9.**

### 2.2 Falsified or overtaken claims (against 15-Jul evidence)

| Doctrine v8 says | 15-Jul reality |
|---|---|
| section 21: connectors = "CSV/Excel + MSSQL/MySQL" | Six provider families realized as live profiles (PG, MySQL, Oracle, SQL Server, FileShare, RestApi); Oracle discovery fixed (PPIQ_SRC) and importing today; Architecture-A remote engine drives all of it |
| section 21: genealogy "Partial" | Chain proven live with explicit keys: cc_slabs(heat_id), hsm_coils(slab_id); H-26014 -> SL-60105 -> C-700394; blended-provenance ledger trigger-enforced |
| section 21: "Demo-through-HMI ... remains the Wave-C target" | The 15-Jul runsheet IS demo-from-empty-through-HMI, executing tonight |
| section 21: L4 statistics "Unstarted" | Pearson+FDR live in the governed run; Spearman/chi-square committed as M2-04 - the row needs regrading, not deletion |
| section 21: access token "stored in browser localStorage" | VERIFY - AuthContext was reworked since 3-Jun; this row may be stale or may be a live G12 finding. One grep decides it; do not ship v8.1 with it unverified |
| INTERNAL CONTRADICTION: section 4.7 grades "Relational databases ... GA" while section 21 says only MSSQL/MySQL exist | Both were wrong at their own issue date in opposite directions; v8.1 must carry ONE connector-status table, generated from the live Connector Truth Contract data, not hand-maintained |
| **THE SUPERVISOR: zero occurrences in 1,215 lines** | Journey step 14, the constitution's keystone, M2's flagship, and v0 exists in code since 14-Jul. The master specification's Engine chapter (sections 6-7) has no closed-loop supervisor. This is the single largest v8.1 addition: a new section 7.x specifying the weekly review loop, the guardrails (never weaken gates), the adjustment-provenance row schema (which also does not exist in the DB yet), dry-run mode, and the known-answer drift test |

### 2.3 v8.1 amendment directives (in order)
1. Add the derivation line: "Derives from and cites concept.md v1.1; where they
   conflict, concept.md wins" - restoring the supremacy chain.
2. Add section 7.x THE SUPERVISOR (content per concept v1.1 step 14; mark v0-built /
   loop-M2 status honestly).
3. Replace section 20 (four waves) and Appendix I with a one-paragraph redirect to
   Roadmap v9 + Backlog. Two living roadmaps in two documents is how plans fork.
4. Retire section 21 as a snapshot ("delta as of 26-Jun") and stop maintaining it -
   the Implementation Audit lineage owns build-vs-doctrine deltas now (12-Jul audit,
   next one post-demo). A hand-maintained delta table in a spec is guaranteed to lie.
5. Regenerate the section 4.7 connector table from the live truth-contract data;
   resolve the 4.7-vs-21 contradiction by construction.
6. Align section 9 tiers to the Part-1.2.B ruling.
7. Verify-or-fix the localStorage token row (one grep of AuthContext).
8. Bump to v8.1 with a changelog naming every amendment above.

---

## PART 3 - The canonical hierarchy going forward

concept.md v1.1 (constitution)
  -> Doctrine v8.1 Part I (the specification - HOW the vision is engineered)
  -> Roadmap v9 (the plan) -> Backlog v23 (the work, frozen ID namespace)
  -> Aspects of Review v4 + Audits (snapshots - superseded, never edited)
  -> rules.txt (founding document - preserved verbatim, formally superseded,
     split from its concatenated appendix)

One rule keeps this healthy: every document carries its derivation line, and any
claim of enforcement or build status must name its evidence (a gate, a report file,
an audit) or be written in the future tense.
