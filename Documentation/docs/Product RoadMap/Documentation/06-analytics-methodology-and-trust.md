# PlantProcess IQ -- Analytics Methodology and Trust

| | |
|---|---|
| Document class | Methodology and Trust |
| Audience | Process and quality engineers, executives, data reviewers |
| Product | PlantProcess IQ (PPIQ) -- SOU Industrial Software |
| Version | 1.0 -- June 2026 |

PlantProcess IQ is built to be trusted by a skeptical engineer. This document explains how its findings are produced and why the way they are framed is deliberate.

## Correlation, not causation

The product performs correlation analysis. It identifies statistical relationships between process parameters and outcomes such as defects, downtime, and KPI movement, and it frames every result as a suspected contributor. It does not assert a guaranteed root cause. This is an honest description of what data analysis over plant history can and cannot establish.

## Population is always stated

Every analysis reports the population it was computed on and the records it excluded -- for example, how many objects of a candidate set carried the data required for the analysis. The product refuses silent survivorship: it never states a driver without stating its population, because the records that were dropped may be exactly where a problem lives. Where the data is not yet sufficient to support an advanced finding, the product shows a readiness state and an honest countdown rather than fabricating an answer.

## Method set

Relationships are assessed with an established set of statistical methods rather than a single naive measure -- rank correlation, mutual information, regularized regression, multicollinearity checks, and bootstrap stability. Using several complementary methods guards against a spurious result from any one of them.

## Multiple-testing control and stratification

When many parameters are tested at once, some will appear related by chance. PlantProcess IQ controls for this with false-discovery-rate control, and it stratifies by visible confounders so that an apparent relationship is not an artifact of a third factor. Where a likely confounder cannot be measured, the product names it rather than ignoring it.

## Blended provenance

Where a finished object draws from more than one upstream source -- for example, a product spanning a transition between two production units -- the product reports a weighted, shared attribution between them rather than assigning it to a single fabricated origin.

## Quantified value with an abstain path

A finding can be expressed as a bounded economic estimate -- a range, computed on the plant's own data, with every input drillable to its source, and with an explicit abstain path when the data does not support a number. This allows a finding to be weighed economically without overstating its precision.

## Grounded assistance

Where a natural-language assistant is provided, deterministic engines compute and rank, and the assistant only explains the result with citations. It cannot present a number that is not grounded in a resolvable piece of evidence, and any claim it makes can be audited back to that evidence.

## The honesty contract, summarized

- Results are suspected contributors and statistical patterns, not guaranteed causes.
- Every result states its method, sample size, filters, and exclusions.
- Insufficient data yields a readiness state, never a fabricated answer.
- A confident wrong answer is worse than an honest "still collecting data," and the product is built to prefer the latter.

This posture is not a limitation to be apologized for. It is the reason the product's findings can be relied upon in a plant where the cost of acting on a wrong conclusion is high.