# PlantProcess IQ - Master Design Document

**Version 4.0 | Author: Karim, SOU Industrial Software, Dusseldorf**

*Covers PPIQ.txt 5.3 and 5.4. Audience 5.8. Voice 5.9.*

---

# CHAPTER 4, PART 3 - CONCURRENCY, LOAD BALANCING, THE GATE AND THE ENGINE

---

# 4.3 MULTI-THREADING AND LOAD BALANCING OF JOBS

## 4.3.1 The problem, stated exactly

> Hundreds of jobs running every two or three minutes, each capable of touching ten million rows. **Incremental import solves the import job only. Every other job class remains large.**

This is the correct statement of the problem and it deserves the arithmetic.

**A plant at the Medium capacity class.** 200 job definitions. Cadence spread between 2 minutes and daily. Say 80 of them run on a 3-minute cadence.

```
80 jobs / 3 min  =  26.7 job starts per minute  =  38,400 starts per day
```

If each start scans even 100,000 rows, that is **3.84 billion rows scanned per day**. If each start opens one connection and holds it for 20 seconds, the steady-state connection demand is `26.7 x 20 / 60 = 8.9` connections just for job starts, before a single user opens a page. If ten jobs happen to align on the same minute - and unmanaged cron-style schedules **always** align, because humans choose round numbers - the instantaneous demand is ten heavy queries at once on a machine sized for two.

**Three failure modes follow, and they are the ones that actually take a server down:**

1. **Thundering herd.** Every job whose cadence divides the hour fires at `:00`. The database sees a spike an order of magnitude above the mean.
2. **Connection exhaustion.** Each concurrent job holds a connection; PostgreSQL's process-per-connection model degrades badly past a few dozen, and the interactive read path starves behind the batch path.
3. **Run pile-up.** A 3-minute job that takes 5 minutes is re-triggered before it finishes. Runs accumulate, each slower than the last, until the machine stops.

**Incremental import is the right first answer and it is not sufficient.** It reduces the *import* class from full-table to delta. It does nothing for feature refresh, correlation scans, model scoring, alert evaluation or report generation, all of which read the accumulated canonical model rather than the delta.

## 4.3.2 The defence stack

Nine mechanisms, layered. Each one alone is insufficient; together they make the load bounded by construction rather than by luck.

| # | Mechanism | Stops |
|---|---|---|
| 1 | Incremental acquisition | Import reading the whole source every cycle |
| 2 | Schedule jitter and coalescing | The thundering herd |
| 3 | Skip-if-running and latest-only | Run pile-up |
| 4 | Admission control with weighted pools | Unbounded concurrency |
| 5 | Per-source load budget | The customer's production database being hammered |
| 6 | Incremental feature refresh | Every analysis rescanning history |
| 7 | Partitioning and retention | Scans growing without limit |
| 8 | Statement timeouts and circuit breakers | One pathological query holding everything |
| 9 | Backpressure and the degradation ladder | Silent collapse under overload |

### Mechanism 1 - Incremental acquisition

Per dataset: a watermark column, a stored cursor, and a read of `WHERE watermark > :last AND watermark <= :now` bounded by the row cap. A batch that hits the cap **advances the cursor to the last row it actually read** and reports itself as partial, so the next cycle continues rather than restarting.

Where a source has no usable watermark, the dataset is marked full-scan and its **minimum cadence is forced to daily**, because a full scan on a 3-minute cadence is not a configuration a plant should be able to create by accident.

### Mechanism 2 - Schedule jitter and coalescing

**Jitter.** Every job's next run time is `base + hash(job_id) mod cadence`. A 3-minute job does not fire at `:00, :03, :06`; it fires at its own offset. **This one line removes the herd.**

**Coalescing.** Two jobs with the same class, the same target and overlapping windows queued within the same tick are merged into one run covering the union. Feature refresh for the same outcome requested by three analyses becomes one refresh.

### Mechanism 3 - Skip-if-running and latest-only

| Policy | Behaviour | Applied to |
|---|---|---|
| **Skip if running** | The tick is dropped and recorded as skipped with the reason | Import, feature refresh |
| **Latest only** | Queued duplicates collapse to the newest request | Alert evaluation, scoring |
| **Queue** | Runs accumulate in order, bounded by queue depth | Reports |
| **Reject** | Refused with a named error | User-triggered runs when the pool is saturated |

A skipped tick is **visible in the monitor with its reason**. Silent skipping is how a plant discovers three weeks later that a job never ran.

### Mechanism 4 - Admission control with weighted pools

The core of the design. **No job runs because its schedule fired. A job runs because a pool admitted it.**

```
POOL              PARALLELISM   ADMITS                          RATIONALE
import                4         import, backfill                network-bound, cheap on CPU
projection            2         canonical projection            write-heavy, contends on indexes
analysis              4         correlation, statistics         read-heavy, bounded by row caps
ml                    1         training, scoring               memory-heavy, one at a time
report                2         report generation, export       bursty, low priority
interactive        reserved     never batch                     the read path is never starved
```

**Weights.** Each job definition carries a `compute_weight` (default 1). A pool admits while `sum(weight of running) + weight(candidate) <= parallelism`. A model-training job weighted 4 occupies its pool alone. **This is why the weight is edited behind a confirmation** that states the resulting utilisation.

**The interactive reservation is the most important line in the table.** A fixed share of the connection pool and of CPU is reserved for user-facing queries. A plant where the dashboard becomes unusable whenever the engine is busy has failed, however correct the engine is.

**Connection discipline.** All pools sit behind a connection pooler. Job workers use a separate pooler identity from the interactive path, so batch work physically cannot exhaust the connections the interface needs.

### Mechanism 5 - Per-source load budget

Enforced **before a read reaches the source**, not after:

| Control | Meaning |
|---|---|
| Max rows per read | Hard cap per statement |
| Statement timeout | Cancelled at the source |
| Requests per minute | Token bucket per connection profile |
| Approved window | Reads refused outside it, with the window named |
| Concurrent reads per source | Usually 1 or 2 |

A read that would breach the budget is **refused with the budget named**, and the refusal is logged like a result. Backfill passes through with its own cumulative rate throttle, so a three-year history load never competes with the live delta.

### Mechanism 6 - Incremental feature refresh

**The mechanism that stops analysis from being the new problem.**

The feature and outcome store is materialised, not computed per run. It refreshes **only for the grains touched by the batches that landed since the last refresh**:

```
refresh_scope = distinct material_unit_id
                FROM canonical rows
                WHERE import_batch_id IN (batches since last_refresh_watermark)
```

An analysis then reads the materialised store rather than rescanning history. **The cost of an analysis becomes proportional to what changed, not to what exists.** Without this, every correlation run at a mature plant scans years of observations, and no amount of pool tuning saves it.

### Mechanism 7 - Partitioning and retention

`parameter_observations` and the results tables are range-partitioned by time, monthly, from the Medium class upward. Consequences: a windowed analysis touches only the partitions in its window; retention is a partition drop rather than a mass delete; index maintenance stays local.

Retention is per stage and configurable: staging short, canonical long, results long, logs per channel.

### Mechanism 8 - Statement timeouts and circuit breakers

Every statement carries a timeout appropriate to its class. A job exceeding it is cancelled and recorded as failed with the timeout named - **never left to run forever holding a connection**.

A **circuit breaker** per source and per job class opens after a threshold of consecutive failures, stops admitting that class, and records why. It half-opens after a cool-down and closes on a success. This is what stops one unreachable source from consuming every import slot with retries.

A **reaper** sweeps runs that are still marked running past a maximum duration and moves them to a terminal reaped state, so no run is stuck forever.

### Mechanism 9 - Backpressure and the degradation ladder

When the system is overloaded it **degrades in a stated order**, visibly, rather than collapsing:

| Level | Trigger | Action | Visible as |
|---|---|---|---|
| 0 Normal | - | All pools at configured parallelism | - |
| 1 Elevated | Queue depth > 2x parallelism | Report and ML pools reduced | A note in the monitor |
| 2 High | Queue depth > 5x, or database load high | Non-critical cadences stretched (3 min becomes 9); coalescing aggressive | Banner: "Analysis is running behind. Cadences temporarily stretched." |
| 3 Critical | Connection or CPU saturation | Only import and interactive admitted; everything else queued | Banner naming what is deferred |
| 4 Protective | Sustained saturation | New user-triggered runs refused with a named error and an estimated wait | The refusal sentence |

**Every level is announced.** A product that quietly stops doing analysis is worse than one that says it is behind.

## 4.3.3 The capacity model

The formula that connects the drivers to the machine, and therefore to the licence envelope (Chapter 1.7.1).

```
Concurrency demand  D  =  SUM over jobs ( weight_j  x  duration_j  /  cadence_j )

Row-scan rate       R  =  SUM over jobs ( rows_scanned_j / cadence_j )

Ingest rate         I  =  SUM over datasets ( delta_rows_d / cadence_d )

Storage growth      G  =  I  x  bytes_per_row  x  retention_days
```

A configuration is admissible when `D <= sum(pool parallelism)` and `R` is within the class's scan budget. **The product computes D and R from the actual job definitions and shows them on the Jobs Administration page beside the configured parallelism**, so an administrator sees the consequence of adding a job before adding it.

**This is also the honest answer to the licence question.** The tier does not buy a number of jobs. It buys a capacity envelope: retained volume, ingest rate, a **minimum refresh interval** (the floor on cadence), weighted compute slots, and concurrent sessions. Three connections pulling a hundred tables every minute and a hundred connections pulling a thousand rows a day are different machines, and only a metered envelope prices them correctly.

## 4.3.4 Telemetry

Per run: queued at, admitted at, started at, finished at, wait time, duration, rows read, rows written, peak memory, pool, weight. Per pool: utilisation, queue depth, admission refusals. Per source: reads, rows, refusals by budget rule, circuit-breaker state.

**Wait time is the number that matters.** Rising wait time is the earliest honest signal that the configuration has outgrown the machine, and it is the number that should drive an upgrade conversation rather than an outage.

## 4.3.5 Acceptance

1. 200 job definitions on mixed cadences run for 24 hours with no pile-up and no run stuck in a running state.
2. Ten jobs scheduled on the same nominal minute start spread across the cadence window.
3. A job that overruns its cadence skips rather than accumulating, and the skip is visible with its reason.
4. The interactive read path stays responsive with every pool saturated.
5. An over-budget read is refused before reaching the source, naming the rule.
6. An unreachable source opens its circuit breaker and does not consume import slots.
7. Forced overload walks the degradation ladder with each level announced.
8. Computed D and R appear on the Jobs Administration page and match measured behaviour.

---

# 4.4 THE GATE AND THE ENGINE

## 4.4.1 The Engine as the hub

Every analytical capability in the product is a client of one engine. There is **one implementation per capability**; a second implementation of any of these is a defect, not an option.

```
                          +---------------------------+
   canonical model  --->  |         THE ENGINE        |
   (ppiq_plant)           |                           |
                          |  1. Feature/outcome store |
                          |  2. Readiness gate        |
                          |  3. Compute engines       |
                          |  4. Results store         |
                          |  5. Value engine          |
                          |  6. Supervisor            |
                          +------------+--------------+
                                       |
       +---------------+---------------+---------------+---------------+
       |               |               |               |               |
   Findings        Risk scores     Suggestions     Value ranges    Assistant
   (D4)            (D5)            (D6)            (D7)            (E1, read-only)
```

**Nothing bypasses it.** A widget may query the canonical model directly for a chart, but no surface computes a statistic, a score or a euro figure of its own. This is what makes every number in the product reproducible from the database.

## 4.4.2 Rules and validation

Validation happens at four points, and each has a different job.

| Point | Validates | Refusal |
|---|---|---|
| **Authoring** | The definition is well-formed: types, joins, cycles, required inputs, registered outcome and grain | At drag time or at save, in the debug log, with the rule named |
| **Admission** | The pool has capacity; the licence allows this job class; the role allows this action | Named error, queued or refused with an estimated wait |
| **Gate** | The **data** can support a defensible answer | Blocked run, persisted, with the failing dimension and its measured value |
| **Result** | The computed result meets the statistical contract | A result that fails the contract is not stored as a finding |

**The critical distinction, and it is the one people get wrong:** authoring validation asks "is this definition legal?"; the gate asks "is this data sufficient?". A perfectly legal definition on insufficient data must be refused, and refused *by the data*, not by the author's judgement.

## 4.4.3 The readiness gate

Five dimensions, three states, evaluated before every analytical run.

| Dimension | Direction | Ready | Partial | Blocked |
|---|---|---|---|---|
| **Independent units** in the window | higher better | >= 60 | >= 30 | below 30 |
| **Outcome events** in the window | higher better | >= 40 | >= 15 | below 15 |
| **Minority-class balance** | higher better | >= 10% | >= 3% | below 3% |
| **Freshness factor** (data age / cadence) | **lower better** | <= 1.0 | <= 2.0 | above 2.0 |
| **Required-field completeness** | higher better | >= 95% | >= 85% | below 85% |

**Six binding clauses.**

1. **The overall state is the worst across dimensions.** Not an average, not a majority. One blocked dimension blocks the run.
2. **Every dimension returns a reason string** built from its measured value and its threshold, for example `42 in [30,60) (Partial)`. That string is why a blocked run is explainable from the database alone, without the application.
3. **The compute engine and the live readiness endpoint call the same function.** The verdict a user sees can never drift from the verdict the engine acts on. A second implementation violates the single-engine law.
4. **Thresholds are per-tenant and governed.** They may be tuned by a human through a change with a recorded justification. They may **never** be lowered to make a run pass, and no automated process may write them.
5. **A blocked run is a persisted run** with a real identifier, a blocked status, the failing dimension and the evidence string. Not an absence, not an error.
6. **The product never shows nothing while a gate is blocked.** It shows the simple analysis that needs no history, the readiness meter with measured counts, and an honest collecting-data state.

**Why these five and not others.** Independent units and outcome events bound sampling error. Minority balance stops a 99-to-1 class ratio producing a meaningless "accuracy". Freshness stops a conclusion drawn on stale data. Completeness stops a conclusion drawn on a column that is mostly null. **Each corresponds to a way a confident wrong answer is normally produced.**

## 4.4.4 Coefficient adjustment as the learning curve grows

> How the system enhances and adjusts its own coefficients as learning accumulates.

This is the Supervisor, and it is deliberately the most constrained component in the product.

### What it adjusts

| Adjustable | Bounds |
|---|---|
| Feature window length per outcome | Within a configured minimum and maximum |
| Lag offsets between a parameter and an outcome | Within a configured range |
| Model hyperparameters | Within a declared search space |
| Job cadence and compute weight | Within the pool's admissible range |
| Feature selection: which features earn a place | From the registered feature set only |
| Stratification variables | From registered dimensions only |

### What it may never touch

- Readiness thresholds.
- Refusal logic.
- Evidence requirements.
- The statistical contract: effect-size ranking, false-discovery control, stability requirements.
- The audit layer.

**Enforcement is by construction, not by convention.** The honesty machinery is outside the Supervisor's write scope: it holds no credential that can write those rows. A convention can be forgotten in a refactor; an absent permission cannot.

### The loop

```
1. OBSERVE     read completed runs, their effects, their stability, their stratum survival
2. PROPOSE     a bounded adjustment with a stated expected improvement
3. DRY-RUN     re-execute against held-out history with the proposed value
4. COMPARE     did the effect strengthen? did stability improve? did anything regress?
5. RECORD      a provenance row: job, parameter, before, after, justification, evidence handle
6. AWAIT       a human approves or rejects. NOTHING CHANGES AUTOMATICALLY
7. APPLY       on approval only; the provenance row is the audit trail
```

### The drift test that gates its release

Inject a known drift into a controlled dataset. The Supervisor must **detect it, propose the correction, and have the dry-run demonstrate recovery**. A Supervisor that cannot pass a known-answer drift test is not released, because a self-tuning component that tunes wrongly is worse than none.

### The learning curve, honestly

| Stage | What the Supervisor can do |
|---|---|
| Weeks 1-2 | Nothing useful. Too few completed runs. It says so |
| Month 1 | First proposals on window length and lag, low confidence, stated as such |
| Month 3 | Feature selection and cadence proposals with measurable dry-run improvement |
| Month 6+ | Stable coefficient sets per outcome; proposals become rare and specific |

**Stating this timeline before the sale is what stops it becoming a disappointment after it.**

## 4.4.5 How the assistant gets its data from the Engine

**The assistant is a read-only client of the Engine. It never computes.**

```
question
   |
   v
[ intent + entity resolution ]  -- plant glossary, synonyms, registry
   |
   +--> [ RETRIEVAL ]  permission-scoped chunks: findings, datasets, mappings, connectors, docs
   |
   +--> [ TOOLS ]      typed, role-scoped, deterministic:
   |                     fetch_finding(id)        -> the stored finding, framing included
   |                     run_kpi(code, filters)   -> the Engine computes; the tool returns
   |                     open_suggestion(id)      -> the stored suggestion with evidence
   |                     material_unit_count(...) -> a count from the canonical model
   |                     readiness(outcome,grain) -> the gate's own verdict
   |
   v
[ GROUNDING ]   every numeric claim must carry a resolvable evidence handle
   |
   v
[ NO-FABRICATION GUARD ]   a sentence containing an uncited number is REJECTED BEFORE DISPLAY
   |
   v
[ EGRESS PLAN ]   decides exactly what may leave the tenant, per serving mode
   |
   v
answer + citations, or a refusal with its reason
```

**Four rules.**

1. **Tools return Engine output. The model phrases it.** `run_kpi` does not ask the model for a number; it calls the deterministic engine and hands the result back.
2. **Retrieval is role-scoped at the chunk level.** Chunks carry a role scope; a viewer's retrieval physically cannot reach an engineer's chunks.
3. **The guard runs before display, not after.** A fabricated figure that reaches the screen and is retracted has already been read.
4. **A refusal is amber and evidential; a transport failure is red and says the request failed.** A transport fault dressed as an evidential abstention is a lie about the product's own state.

**What the assistant may never do:** compute, rank, originate a figure, write anything except its audit log, or answer outside the retrieval scope its role permits.

## 4.4.6 The Engine as the hub for AI and ML

Statistical jobs, model training, model scoring, suggestion generation and value computation are **all job classes on the same substrate**, sharing:

| Shared | Consequence |
|---|---|
| The feature and outcome store | A model and a correlation see the same features. They cannot disagree about the data |
| The readiness gate | An ML run is refused on insufficient data exactly as a statistical one is |
| The results store | Findings, scores and suggestions are queryable together and by the assistant |
| The job executor | ML competes for slots under the same admission control |
| The provenance model | Every output traces to its run, its definition version and its population |

**Jobs feed each other.** A correlation identifies candidate features; a model consumes them; scores generate suggestions; the value engine prices them; the Supervisor reviews all of it. **The hub is what makes that a loop rather than five disconnected tools.**

## 4.4.7 Acceptance

1. The gate blocks and completes correctly against known datasets, and the overall state equals the worst dimension every time.
2. The live readiness endpoint and the engine return identical verdicts for identical inputs.
3. A blocked run is reconstructable from the database alone, with no application running.
4. A threshold cannot be written by any automated path; the attempt is refused and audited.
5. The Supervisor's dry-run demonstrates recovery on injected drift.
6. Results counts before and after a Supervisor run are identical.
7. The assistant answers with resolving citations, refuses without evidence, and shows red for transport failure and amber for abstention.
8. No surface computes a statistic outside the Engine.

---

*End of Chapter 4, Part 3.*
