# PlantProcess IQ - Master Design Document

**Version 4.0 | Author: Karim, SOU Industrial Software, Dusseldorf**

*Covers PPIQ.txt 5.5, 5.6 and 5.7. Audience 5.8. Voice 5.9.*

---

# CHAPTER 4, PART 4 - STATISTICS, MACHINE LEARNING, AND THE ASSISTANT

---

# 4.5 STATISTICS, CORRELATION AND DATA ANALYSIS

## 4.5.1 How to read this catalogue

Every function is a **block on the S3 wiring diagram**. Each entry states, exactly as the guideline requires:

```
BLOCK          the palette name
INPUTS         what wires into it, with port types
CONFIG         what is set inside it (expression editor, dropdowns)
OUTPUTS        what wires out, with port types
VALIDATES      the rule that must hold, and the refusal sentence if it does not
BEST CHART     the chart that displays this function's output correctly
```

**The BEST CHART column is binding on the widget layer.** When a user charts an analysis result, the chart-type switcher defaults to the block's declared best chart and offers the alternatives it declares. This is how the wiring diagram and the analysis page stay coherent: **the block declares how its output should be seen.**

All blocks are registry rows, extensible without a code branch. All obey the statistical honesty discipline of Chapter 1.5.8, always on and never bypassable.

## 4.5.2 Group A - Descriptive

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Summary statistics** | dataset | measure column | table: n, mean, median, sd, min, p25, p75, max, nulls | Column is numeric. *"`<col>` is text; Summary statistics needs a number."* | **Table**; box plot as alternative |
| **Distribution** | dataset | measure, bin count or auto | dataset: bin, count | Numeric; bins >= 2 | **Histogram**; box plot |
| **Category counts** | dataset | dimension, optional measure | dataset: category, count or aggregate | Cardinality <= 500 else warn *"`<col>` has `<n>` distinct values; the chart will be unreadable. Group them first."* | **Bar**; pareto when the tail is long |
| **Time series** | dataset | time column, measure, aggregation, bucket | dataset: bucket, value | Time column is a date type; bucket >= source resolution | **Line**; area for cumulative |
| **Cross-tabulation** | dataset | two dimensions, measure | matrix | Both cardinalities <= 50 | **Heatmap**; pivot table |
| **Outlier detection (IQR / z-score)** | dataset | measure, method, threshold | dataset with `is_outlier` flag | n >= 20 | **Box plot**; scatter with outliers highlighted |
| **Missingness profile** | dataset | - | dataset: column, null count, null percent | - | **Bar**, descending |

## 4.5.3 Group B - Association and correlation

**The core group. This is what the product exists to compute.**

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Pearson correlation** | dataset | two measures, or one measure against a measure set | r, p, n, confidence interval | Both numeric; n >= 30; **warns on strong non-linearity** *"The relationship looks non-linear; Spearman may fit better."* | **Scatter** with fitted line; heatmap for a matrix |
| **Spearman rank correlation** | dataset | two measures | rho, p, n | Both ordinal or numeric; n >= 30 | **Scatter** of ranks; heatmap for a matrix |
| **Correlation matrix** | dataset | measure set, method | matrix of coefficients with q-values | Set size <= 200; **all pairs pass through false-discovery control** | **Heatmap**, diverging palette centred on zero |
| **Chi-square independence** | dataset | two dimensions | chi2, p, degrees of freedom, Cramer's V, contingency table | Every expected cell >= 5, else *"`<n>` cells have an expected count below 5. Merge categories or widen the window."* | **Heatmap** of standardised residuals; stacked bar |
| **ANOVA / Kruskal-Wallis** | dataset | dimension (groups), measure | F or H, p, eta-squared, per-group means | >= 2 groups, each n >= 10; normality checked and the non-parametric alternative substituted with a note | **Box plot** per group; bar of group means with error bars |
| **Odds ratio / relative risk** | dataset | binary outcome, binary or binned exposure | OR, confidence interval, p, 2x2 table | Every cell >= 5 | **Forest plot**; bar with confidence intervals |
| **Point-biserial** | dataset | binary outcome, measure | r, p, n | Outcome exactly two values | **Box plot** by outcome class |
| **Lagged correlation** | dataset | parameter, outcome, lag range | coefficient per lag with the best lag marked | Time column present; lag range within the window | **Line** of coefficient against lag |
| **Genealogy-attributed correlation** | dataset | parent-grain parameter, child-grain outcome | coefficient weighted by contribution weight, effective n | **Weights per child must sum to 1.0**; else *"Attribution weights for `<n>` units do not sum to 1. Fix the genealogy mapping before correlating across grain."* | **Scatter** with point size by weight |

**Genealogy-attributed correlation is the block no business-intelligence tool has.** It is the mechanism behind the product's central claim: a melt parameter related to a coil defect, correctly weighted where a coil descends from two heats.

## 4.5.4 Group C - Discipline (always applied, never optional)

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **False-discovery control** | p-value set | q threshold (default 0.05) | q-values, significance flags | Set size >= 2 | **Table** with significance; volcano plot |
| **Effect-size ranking** | results set | - | ordered by absolute effect, p as tie-break only | **Refuses to order by p-value** | **Bar** of effect size, descending |
| **Stratification** | dataset, results set | stratification dimensions | per-stratum effect, survival verdict, reason | Each stratum n >= 15, else the stratum is reported as under-powered rather than dropped silently | **Forest plot** by stratum |
| **Bootstrap stability** | dataset, results set | resamples (default 1000) | point estimate, lower, upper, sign consistency, stable flag | n >= 30 | **Interval plot**; histogram of resampled estimates |
| **Confounder check** | dataset, candidate confounders | - | effect before and after adjustment, delta | Confounders registered dimensions | **Slope chart** before to after |

**These five are not user-selectable steps.** They are applied to every association result, and their outputs are stored with the finding as data. A user may inspect them; a user may not switch them off.

## 4.5.5 Group D - Process and quality specific

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Control chart** | dataset | measure, subgroup, chart kind | centre line, control limits, violations by rule | n >= 25 subgroups | **Control chart** (line with limit bands) |
| **Capability** | dataset | measure, specification limits | Cp, Cpk, Pp, Ppk | Specification limits present; approximate normality checked | **Histogram** with specification limits |
| **Pareto of causes** | dataset | cause dimension, measure | descending contribution with cumulative percentage | - | **Pareto** |
| **Yield decomposition** | dataset | stage dimension, good and total measures | yield per stage, cumulative | Stages ordered | **Waterfall** |
| **Downtime impact split** | downtime dataset | - | stopped minutes and **production-impact minutes** side by side per cause | **Both quantities present**; else *"This dataset has no production-impact minutes. Impact cannot be computed from stopped minutes alone."* | **Grouped bar**, two series |
| **Transition analysis** | dataset with genealogy | - | outcome rates for transition versus non-transition units | `is_transition` present | **Bar** with confidence intervals |
| **Window comparison** | dataset | two windows, measure | difference, confidence interval, significance | Both windows non-empty | **Bar** with intervals; slope chart |

## 4.5.6 Validating the block diagram

The rules the S3 validator enforces at drag time, in addition to the universal wiring rules of Part 2:

| Rule | Refusal |
|---|---|
| A statistical block's measure input must be numeric | "`<col>` is text; `<block>` expects a number." |
| A grouping input must be a dimension of acceptable cardinality | "`<col>` has `<n>` distinct values; grouping needs fewer than `<limit>`." |
| An outcome must be registered in the outcome registry | "`<outcome>` is not a registered outcome. Register it or choose another." |
| Cross-grain analysis requires genealogy | "`<parameter>` is at `<grain A>` and `<outcome>` is at `<grain B>`. Add a Genealogy attribution block between them." |
| A discipline block cannot be deleted from the chain | "False-discovery control is applied to every association result and cannot be removed." |
| Sample-size preconditions are checked before Run | "This definition needs 30 units; the current window has 18. Widen the window or wait." |

**Every one of these is checked before the run, not after.** The author learns the constraint while authoring, which is the entire point of a debug log with described severities.

---

# 4.6 AI AND MACHINE LEARNING

## 4.6.1 Position

Machine learning is the **fourth and fifth capability layers**: prediction and recommendation. It runs on the same feature store, behind the same readiness gate, under the same job executor and the same honesty contract. **It is not a separate product and it never bypasses the gate.**

Licence: Pro Plus upward.

## 4.6.2 Group E - Feature engineering blocks

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Feature assembly** | canonical dataset | grain, window, feature list from the registry | feature matrix: unit, features, label | Every feature registered; grain declared | **Table**; heatmap of feature coverage |
| **Genealogy roll-up** | parent-grain features | aggregation (weighted mean, min, max, sum) | features at child grain | Weights sum to 1.0 per child | **Bar** of contribution per parent |
| **Lag feature** | time series | lag steps | lagged columns | Time column present | **Line** with lagged overlay |
| **Rolling window feature** | time series | window, statistic | rolling column | Window <= series length | **Line** with band |
| **Binning** | measure | bin strategy, count | ordinal column | Numeric | **Histogram** with bin edges |
| **Encoding** | dimension | one-hot or ordinal | encoded columns | Cardinality <= 50 for one-hot | **Bar** of category frequency |
| **Missing-value policy** | feature matrix | drop, impute mean/median, flag | matrix plus indicator columns | **Refuses silent imputation**: the policy is explicit and recorded with the model | **Bar** of missingness before and after |
| **Scaling** | feature matrix | standard or min-max | scaled matrix plus stored parameters | Parameters stored with the model so scoring reuses them | **Box plot** before and after |

**The missing-value policy block is deliberately mandatory.** A model trained on silently imputed data produces a confident wrong answer, which is the exact failure this product exists not to commit.

## 4.6.3 Group F - Model blocks

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Train/validation split** | feature matrix | strategy: **time-based** (default) or stratified random; ratio | two matrices | **Time-based is the default and random is warned**: *"Random splitting leaks future information in a process dataset. Time-based split recommended."* | **Timeline** of the split |
| **Classification model** | training matrix | algorithm, hyperparameters | model artifact, metrics | Minority class >= 3 percent; n >= gate minimum | **ROC curve** and **confusion matrix** |
| **Regression model** | training matrix | algorithm, hyperparameters | model artifact, metrics | n >= gate minimum | **Predicted-versus-actual scatter**; residual plot |
| **Anomaly detection** | matrix | method, contamination | anomaly score per unit | n >= 100 | **Scatter** with anomalies highlighted; time series with markers |
| **Clustering** | matrix | method, k or auto | cluster label, silhouette | n >= 50 | **Scatter** on two components, coloured by cluster |
| **Feature importance** | trained model | method (permutation preferred) | importance per feature with confidence | Model trained; **permutation importance preferred over impurity**, which is biased toward high-cardinality features | **Bar**, descending, with intervals |
| **Partial dependence** | trained model, feature | grid resolution | response curve | Feature in the model | **Line** with confidence band |
| **Model evaluation** | model, validation matrix | metric set | accuracy, precision, recall, F1, AUC, or RMSE, MAE, R-squared | Validation set untouched by training | **ROC**, **precision-recall**, **calibration plot** |
| **Calibration** | model, validation matrix | method | calibrated model | Classification only | **Calibration plot** |
| **Scoring** | model, live matrix | - | score, class, drivers per unit | Feature schema matches training exactly, else *"The model was trained on `<n>` features; this dataset provides `<m>`. Retrain or align."* | **Distribution** of scores; **table** of top-risk units |

## 4.6.4 Group G - Prediction and recommendation

**This is the capability the guideline describes: predict early, then remediate downstream.**

| Block | Inputs | Config | Outputs | Validates | Best chart |
|---|---|---|---|---|---|
| **Early-stage risk score** | scoring output at an early grain | horizon, threshold | risk score and class per unit, with drivers | Unit has not yet reached the outcome stage | **Table** of at-risk units; **distribution** of scores |
| **Downstream remediation search** | risk output, historical practice dataset | candidate later-stage practice set | ranked practices with historical outcome rates and support | **Each candidate needs >= 20 historical cases**, else it is reported as insufficient support rather than recommended | **Bar** of outcome rate by practice, with support shown |
| **Practice comparison** | historical dataset | practice dimension, outcome | outcome rate per practice with confidence | Every practice n >= 20 | **Forest plot** |
| **Suggestion generation** | risk output, remediation output | thresholds | suggestions with evidence handles and expected effect | **Every suggestion carries resolvable evidence**; one without is not emitted | **Card list** with evidence links |
| **Value attachment** | suggestion, cost assumptions | - | bounded euro range | Cost inputs present, else `InsufficientBasis` | **Interval bar** |

**The remediation block is the product's most valuable output and its most dangerous.** A recommendation to change a later-stage practice is an instruction to a plant. Three constraints follow, and each is enforced:

1. **A minimum historical support of 20 cases.** Below that the block reports insufficient support and recommends nothing.
2. **Evidence handles on every suggestion**, resolving to the historical cases that justify it.
3. **Human approval before anything is acted on.** The product suggests. It never instructs and never acts.

## 4.6.5 Model governance

| Requirement | Rule |
|---|---|
| Registry | Every trained model is registered with version, algorithm, feature list, training window, split strategy, missing-value policy, scaling parameters and metrics |
| Reproducibility | A registered model can be retrained to the same result from the recorded definition |
| Drift monitoring | Feature distribution and performance monitored; drift beyond a threshold moves the model to a review state and **stops scoring** |
| Retirement | A retired model stops scoring; its historical scores remain readable and labelled with the retired version |
| Determinism | Scoring is deterministic. The same input and the same model version produce the same score |
| **No language model in the compute path** | Recorded as data on every result |

## 4.6.6 Validating the ML block diagram

| Rule | Refusal |
|---|---|
| Feature schema mismatch at scoring | Named, with the counts on both sides |
| Validation set touched by training | "The validation set overlaps the training set by `<n>` rows." |
| Random split on a time-ordered dataset | Warning with the leakage explained |
| Class imbalance below the gate minimum | Blocked by the gate, with the measured balance |
| Silent imputation | Refused; a missing-value policy block is required |
| Recommending on insufficient support | Reported as insufficient support, never as a recommendation |

---

# 4.7 THE AI ASSISTANT

## 4.7.1 The form factor: a persistent dock, not a page

> **A chat box at the inline-end, block-end corner, present on every page.**

This is a correction to an earlier specification that treated the assistant as a destination page. **It is not a page. It is a dock.**

| State | Appearance | Behaviour |
|---|---|---|
| **Collapsed** (default) | A circular launcher, 56 px, inline-end and block-end, offset 24 px, Electric Blue with the assistant glyph | Unread-answer badge; hover reveals "Ask about this page" |
| **Expanded** | A panel 400 px wide, 600 px tall, anchored to the same corner, Panel Navy with a 1 px Industrial Blue border and a soft shadow | The conversation, the composer, the evidence strip |
| **Docked wide** | 640 px, pinned; page content reflows rather than being covered | For an extended investigation |
| **Full** | The whole viewport | Only when the user chooses it |

**Rules that make it liveable:**

- **It persists across navigation.** Moving from the workspace to Findings does not lose the conversation.
- **It never covers a primary action.** Collapsed it occupies a corner; expanded it offsets the page's floating controls.
- **Its state is per user and remembered**: collapsed or expanded, width, and the last conversation.
- **Escape collapses it. A keyboard shortcut opens it and focuses the composer.**
- **On mobile it becomes a full-height sheet** rather than a floating panel.
- **It mirrors correctly** in a right-to-left locale: the dock moves to the other inline edge, because its position is expressed as inline-end and never as "right".

## 4.7.2 Page context awareness

**The dock knows what page it is on, and this is most of its value.**

On open, the client sends a context envelope: the route, the page definition code, the current associative selection, the visible time window, and the selected entity if there is one.

| Where it is opened | What it offers unprompted |
|---|---|
| Interactive Workspace | "This page is filtered to `<selection>`. Ask about what you are seeing." |
| Findings | "Ask why this finding was ranked first, or what it is worth." |
| Genealogy Explorer | "Ask about this unit's ancestry or its parameters." |
| Jobs Monitor | "Ask why a job was blocked." |
| Transformation Studio | "Ask what a block does or why a wire was refused." |

**Context narrows retrieval; it never widens permission.** A user who cannot see a page's data cannot reach it by asking from that page.

## 4.7.3 Composition and the honesty contract

```
question + page context
   |
   v
[ intent + entity resolution ]     glossary, synonyms, registry
   |
   +--> [ RETRIEVAL ]   role-scoped chunks: findings, datasets, mappings, connectors, documents
   |
   +--> [ TOOLS ]       typed, role-scoped, deterministic - the Engine computes, the tool returns
   |
   v
[ GROUNDING ]           every numeric claim carries a resolvable handle
   |
   v
[ NO-FABRICATION GUARD ]  a sentence with an uncited number is REJECTED BEFORE DISPLAY
   |
   v
[ EGRESS PLAN ]         what may leave the tenant, per serving mode
   |
   v
answer + citations   |   or a refusal with its reason
```

**The four rules.** Tools return Engine output and the model only phrases it. Retrieval is role-scoped at the chunk level. The guard runs before display. And **a refusal is amber and evidential while a transport failure is red and says the request failed** - a transport fault dressed as an abstention is a lie about the product's own state.

## 4.7.4 The panel, region by region

| Region | Contents |
|---|---|
| **Header** | Title "Assistant", context chip naming the current page, expand-width, full-screen, close |
| **Conversation** | Messages newest at the block-end. Each answer carries citation chips beneath it |
| **Citation chip** | Electric Cyan, labelled with the evidence kind and its identifier. Click opens the evidence strip |
| **Evidence strip** | Slides from the block-end: the finding, the run, the population, the rows. Includes **Open in page** |
| **Composer** | Auto-growing text area, Enter sends, Shift-Enter newlines, Send button inline-end |
| **Suggested questions** | Three context-derived starters on an empty conversation, from the registry, never a hardcoded list |
| **Footer note** | "Answers are assembled from your plant's data with citations. Figures are computed by the engine." |

## 4.7.5 States

| State | Rendering |
|---|---|
| Thinking | Streamed, with the tool being used named: "Reading findings...", "Computing KPI..." |
| Answered | Text plus citation chips |
| **Refused** | **Amber** card: "I don't have evidence for that." plus what would answer it |
| **Transport failed** | **Red** card: "Request failed." plus Retry |
| Out of scope | "That is outside the data I can see for your role." |
| Index empty | "Nothing is indexed yet." Administrators get a Reindex action |
| Tier locked | The dock is absent below Pro Plus, not present and broken |

## 4.7.6 Configuration, from the interface

Per Rule 1's configurability corollary, everything is administered from E2: tools per role and per tier, indexed knowledge sources, the plant glossary and its synonyms, guardrail phrases, the citation ceiling, verbosity, and the serving mode (self-hosted, private endpoint, customer model) with the per-tenant no-egress control.

## 4.7.7 What the assistant may never do

Compute. Rank. Originate a figure. Write anything except its audit log. Answer outside its role's retrieval scope. Render a number without a resolvable citation. **Dress a transport failure as an evidential abstention.**

## 4.7.8 Acceptance

1. The dock is present, collapsed, on every page, and persists across navigation.
2. Opening it on a filtered workspace offers context-aware starters naming the selection.
3. A grounded question returns cited answers whose citations resolve to real rows.
4. An unanswerable question refuses in amber; a stopped API fails in red.
5. Stopping and restarting the API recovers without a page reload.
6. A viewer cannot retrieve engineer-scoped chunks.
7. Every figure in an answer matches the Engine's stored value exactly.
8. The dock mirrors to the other inline edge in a right-to-left locale.
9. Below Pro Plus the dock is absent rather than broken.

---

*End of Chapter 4, Part 4. Chapter 4 is complete: Part 1 the analysis page, Part 2 the authoring shell, Part 3 concurrency and the engine, Part 4 statistics, machine learning and the assistant.*
