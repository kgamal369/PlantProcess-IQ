# T-007 - EMULATION INVENTORY AND THE 36-CHART BLUEPRINT

**Task:** T-007 | **Milestone:** M1 | **Phase:** M1-P1 | **10 h**
**Save as:** `Documentation/docs/Product Book/M1_Emulation_Inventory_and_Chart_Blueprint.md`
**Date:** 2 August 2026
**Part 1 of 2.** Part 2 builds the phenomenon matrix and classifies every widget. **Nothing touches the generator until both parts are signed.**

**Sources read, not assumed:** `docs/emulation/FLEET_RELATIONS.md`, `docs/demo/demo-source-systems.md`, `scripts/demo/Seed-PresentationDashboards.v2.ps1`, Chapter 4 5.1.5.

---

## PART A - WHAT THE EMULATION ALREADY CARRIES

### A.1 Scale, measured

| Structure | Count |
|---|---:|
| Heats | 1,802 |
| Casting sequences | 956 |
| Slabs | 18,661 |
| Coils | 18,661 |
| HSM stand passes | 111,966 |
| Surface defects, 20 codes | 34,312 |
| Pickled coils | 15,782 |
| QA tests | 8,920 |

Three months, `seed=42`, deterministic.

### A.2 Genealogy and conservation

`heat_id -> cc_slabs.slab_id -> hsm_coils.coil_id -> parsytec / pkl / QA / yard`, grade inherited down the chain.

Conservation is enforced in the generator, not asserted in a document: `coil_width = slab_width - 2..6 mm`; `coil_weight = slab_weight * 0.985`; `coil_length = slab_length * thickness_ratio * 0.985`.

### A.3 The eight source systems

Postgres MeltShop, Oracle Caster, Oracle HSM, MSSQL PKL, MySQL Downtime, MySQL Parsytec, Excel Yard, Excel QA. No source publishes a host port; the product connects by service name. **The heterogeneity is the demonstration** - four engines and two file sources proving this is a configurable platform rather than one hardcoded schema.

### A.4 The seventeen planted relations, with the effect sizes that are recorded

| Id | Relation | Measured |
|---|---|---:|
| R1 | CRACK_LONG ~ peritectic carbon band x superheat x casting speed | **9.3x** |
| R2 | INCLUSION ~ scrap and DRI ratio + low aluminium + tundish age | **4.5x** |
| R3 | WAVY_EDGE ~ rolling force per gauge + roll wear | **9.5x** |
| R4 | SLIPPAGE_MARK ~ lubrication viscosity outside the 33-50 cSt window | **28.9x** |
| R5 | ROLL_MARK ~ roll campaign age since roll change | **3.8x** |
| R6 | EDGE_CRACK ~ sulfur + low finishing temperature | not recorded |
| R7 | SCALE_ROLLED ~ high finishing temperature + thick gauge | not recorded |
| R8 | CRACK_TRANS ~ peritectic + niobium microalloying | not recorded |
| R9 | SLIVER, LAMINATION, BLISTER ~ scrap ratio and nitrogen | not recorded |
| R10 | PINHOLE, OSCILLATION_MARK ~ superheat and casting speed | not recorded |
| R11 | GAUGE_DEV ~ slippage events; WIDTH_DEV ~ campaign wear | not recorded |
| R12 | oxygen_ppm ~ scrap ratio, meltshop-internal | not recorded |
| R13 | electricity_mwh ~ scrap ratio + cold furnace + bucket count | not recorded |
| R14 | power_on_min ~ buckets + scrap ratio + furnace state | not recorded |
| R15 | breakout probability ~ superheat + scrap ratio, giving a 4-6 h production stoppage | not recorded |
| R16 | HSM cobble downtime ~ slippage, with the transfer buffer absorbing the first 45 min | not recorded |
| R17 | PKL line speed ~ inverse gauge; QA yield and UTS ~ grade family | not recorded |
| **C** | **SCRATCH (1.0x), DENT, SEAM - pure noise, MUST NOT correlate** | **1.0x** |

**R4 at 28.9x is the strongest and it is worth a caution.** A 29-fold effect in a plant dataset is close to deterministic, and a customer engineer may read it as planted rather than discovered. Part 2 should decide whether to demonstrate R4 at all, or to lead with R2 at 4.5x, which is the most believable of the five measured.

### A.5 Systemic structure already present

Chemistry by grade across **12 elements x 3 sample stations** (EAF, LF, trim). Additive quantities by grade. Sequence grouping by grade family. **Crew rotation.** Ladle reline counters. Cold-furnace and maintenance reheating. Downtime propagation with equipment-versus-production stoppage semantics. Roll wear. Lubrication windows.

---

## PART B - WHAT THE DASHBOARDS ACTUALLY SHOW TODAY

Twenty-nine widgets across seven dashboards, read from the seed script.

| Dashboard | Widgets | Chart types used |
|---|---:|---|
| PRODUCTION_OVERVIEW | 8 | 4 kpi, line, donut, area, table |
| QUALITY_MONITORING | 4 | line, bar, donut, table |
| EQUIPMENT_OPERATIONS | 4 | bar, line, table, bar |
| CORRELATION_FINDINGS_BOARD | 2 | line, bar |
| PARAMETER_DEEP_ANALYSIS | 5 | 2 kpi, line, bar, table |
| RISK_INTELLIGENCE | 4 | kpi, line, bar, table |
| MODEL_INSIGHTS | 2 | line, donut |

### B.1 The finding that matters most

**Every one of the twenty-nine widgets binds to eight generic dimensions and six generic measures:**

- Dimensions: `day`, `week`, `month`, `materialUnitType`, `defectType`, `severity`, `equipment`, `parameterCode`
- Measures: `materialCount`, `observationCount`, `defectCount`, `defectRate`, `riskScore`, `avgParameterValue`

**Not one widget touches grade, shift, crew, superheat, casting speed, finishing temperature, chemistry, gauge, roll campaign age, tundish age, lubrication viscosity, scrap ratio, coil position, or the downtime split.**

That is the whole gap in one sentence. The emulation carries a three-month flat-steel plant with seventeen planted relations, and the dashboards show counts by day and by type. **The dataset is not the problem. The binding is.**

### B.2 Two honesty defects found while reading the seed

**`MI_SEV` is titled "Predicted Severity Mix" and is a donut of `defectCount` by `severity`.** Nothing predicted it. It is a count of what already happened, labelled as a prediction. Under the no-fake-answer rule this is Severity 1 and it must be retitled or rebuilt before the page is shown - and Model Insights is exactly the page where an overclaim costs most, because its value is the honest readiness refusal.

**`CORRELATION_FINDINGS_BOARD` has two widgets and neither shows a correlation.** `CF_RATE` is a defect-rate line by day; `CF_TOP` is a defect-count bar by type. A page named Correlation Findings currently contains no finding, no effect size, no q-value and no population. It is the weakest page against the strongest available data.

---

## PART C - THE 36-CHART BLUEPRINT

Six primary pages, six charts each. The seventh dashboard stays as the technical backup.

**For each chart: what it must SHOW, and what would make it boring.** The second column is the specification - the data has to beat it.

### Page 1 - Production and Operating Practice

| # | Chart | Must show | Boring if |
|---|---|---|---|
| 1 | Production volume trend, line | Strong and weak days, visible weekly rhythm | A near-flat line |
| 2 | Production by shift, stacked column | Three shifts with genuinely different mixes | Three bars within 5 percent |
| 3 | Throughput by grade, bar | A real product mix, several grades carrying volume | One grade at 80 percent |
| 4 | Production by equipment, bar | Different contribution per caster and per mill line | One unit, or an even split |
| 5 | Heat-to-coil cycle time trend, line | Faster and slower periods, and a visible regime boundary | Uniform noise around a mean |
| 6 | Production detail, table | Heat, slab, coil, grade, shift, weight on one row | Only counts, no genealogy |

### Page 2 - Quality, Defects and Chemistry

| # | Chart | Must show | Boring if |
|---|---|---|---|
| 7 | Defect rate trend, line | Stable periods punctuated by excursions | Flat, or pure noise |
| 8 | Defect Pareto, bar | Six to ten meaningful classes from the 20 available | Three bars |
| 9 | Defects by grade, stacked bar | Some grades materially more sensitive | Even across grades |
| 10 | Defects by production unit, bar | An equipment or context effect | **One row - which is what this returns today** |
| 11 | Surface defect position map, heatmap | Clusters along coil length and across width | Uniform scatter |
| 12 | Chemistry conformance, conditional table | Actual against **grade specification** minima and maxima, cells flagged | All rows inside band, or limits hardcoded in the product |

Chart 12 is the low-code showcase: the limits must come from the customer's grade specification data, and the formatting rule must be editable in the interface in front of the customer.

### Page 3 - Equipment, Maintenance and Downtime

| # | Chart | Must show | Boring if |
|---|---|---|---|
| 13 | Downtime by equipment, Pareto | A clear loss concentration | Even distribution |
| 14 | Downtime by shift, stacked column | An operating-practice difference | Equal thirds |
| 15 | **Equipment stopped against production lost, paired column** | The buffer and cascade distinction: stopped 145, impact 92, buffer 53 | The two bars equal - which means the distinction was never modelled |
| 16 | Downtime reason Pareto | Breakdown, cobble, changeover, maintenance as separate reasons | One catch-all reason |
| 17 | Cycle time after maintenance, box plot | A decaying difference across the first three to five heats | No difference - the regime is not planted yet |
| 18 | Equipment performance by campaign age, line | Gradual degradation across a campaign | A step, or a flat line |

Chart 15 is the single most distinctive chart in the set. Equipment stoppage is not production stoppage, and almost no BI tool in this market shows the difference.

### Page 4 - Process Parameter Analysis

| # | Chart | Must show | Boring if |
|---|---|---|---|
| 19 | Parameter trend, line | Regimes and shift changes, not noise | Random walk |
| 20 | Parameter distribution, histogram | A believable operating distribution with tails | A perfect normal, or a uniform block |
| 21 | Parameter by grade, box plot | Different bands per grade | Identical boxes |
| 22 | Parameter by shift, box plot | Overlapping distributions with different **variance** | Three cleanly separated means, which reads as fabricated |
| 23 | Parameter against outcome, scatter | A continuous relationship with real spread | A straight line, or a cloud |
| 24 | Outlier detail, conditional table | Individual coils outside the operating window, drillable | Aggregates only |

Chart 22 carries the shift story: the means must overlap and the variance must differ. Three tidy separated means would look planted.

### Page 5 - Correlation and Statistical Findings

| # | Chart | Must show | Boring if |
|---|---|---|---|
| 25 | Parameter by outcome correlation heatmap | 10-15 parameters against outcomes, mixed strengths | All cells strong, or all weak |
| 26 | Ranked contributors, bar | Strongest observed associations by **effect size** | Ranked by p-value |
| 27 | Casting speed against defect, scatter | The continuous relation behind R1 | No visible gradient |
| 28 | Superheat by casting speed, heatmap | The **interaction** - a hot region, not a gradient | A flat surface |
| 29 | **Conditioned against unconditioned, paired result** | An association that shrinks materially once grade is conditioned on | No change either way |
| 30 | Findings table | n, effect, q after Benjamini-Hochberg, method, evidence link | Correlation coefficients with no population |

Chart 29 is the honesty moment and the hardest to fake. It needs a real confounder, which R1 and the grade families already provide.

### Page 6 - Intelligence, Risk and Model Readiness

| # | Chart | Must show | Boring if |
|---|---|---|---|
| 31 | Readiness dimensions, status cards | Five dimensions, each with **the measured value beside its threshold** | A green tick with no number |
| 32 | Population by context, bar | How much evidence exists per cohort | One total |
| 33 | Risk or finding trend, line | The current deterministic engine output | A prediction the engine did not make |
| 34 | Drivers and contributors, ranked table | Evidence-backed ranking with provenance | An unsourced list |
| 35 | Comparable units, evidence table | Individual coils and heats, drillable to source | Aggregates |
| 36 | Engine state and stability | An honest Ready, Partial or Blocked with its reason | **Anything titled "predicted" that was not predicted** |

Chart 36 replaces `MI_SEV`. If the model is not ready, say why in five dimensions - that refusal is the selling point, and a fabricated curve destroys it.

---

## PART D - WHAT THIS TELLS US BEFORE PART 2 RUNS

**Of 29 existing widgets, roughly 8 to 10 map onto a blueprint chart without change.** The rest are bound to generic counts and need rebinding, not rebuilding. Roughly 26 to 28 of the 36 charts do not exist today.

**But almost none of that gap is a data gap.** Casting speed, superheat, finishing temperature, grade, chemistry, gauge, campaign age and the downtime split are all in the sources already. They are simply not bound to any widget, and several are not projected into canonical parameters at all.

Three charts are genuinely blocked on missing data, and only three: **17** needs the post-maintenance recovery regime, **22** needs the shift variance regime, and **12** needs the grade specification table. Those are exactly the three enrichment tasks in v2.4, and no others are justified until Part 2 proves otherwise.

**Chart 10 already fails today** - Defects by Equipment returns one row. That is canonical equipment attribution, not a data shortage, and it must be diagnosed before anyone touches the generator.

---

## SIGN-OFF

| Question | Answer |
|---|---|
| Is the 36-chart blueprint accepted as the dataset specification? | |
| Do we demonstrate R4 at 28.9x, or lead with R2 at 4.5x as more believable? | |
| `MI_SEV` "Predicted Severity Mix" - retitle, rebuild as chart 36, or remove? | |
| Is the seventh dashboard confirmed as technical backup rather than a shown page? | |

**Part 2 does not start until these four are answered, and the generator is not touched until Part 2 is signed.**
