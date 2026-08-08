# T-074 CLOSURE - Registry-typed quantity guard

**Status:** Complete. Closed.
**Commit:** `0786a99c`
**Evidence:** `docs/m1/evidence/T-074_quantity_certification_20260808_212511.txt`

## What was built

`parameter_definitions` is the sole authority for a quantity's value type, unit,
sign and range. No table, no migration and no second dictionary were added.

    question
    -> resolve an unambiguous active registry quantity
    -> model produces draft and claims
    -> TypedQuantityGuard validates quantity semantics
    -> existing GroundingService performs numeric and provenance grounding
    -> grounded answer or honest refusal

## Three outcomes, not two

| Outcome | Behaviour |
|---|---|
| `NoMatch` | the existing path is untouched |
| `Resolved` - exactly one active, non-deleted, non-synthetic definition | the guard is mandatory |
| `KnownButUntrustedOrAmbiguous` - synthetic-only, tied or conflicting | a numeric answer fails closed |

Approved rows are considered alone rather than ranked alongside synthetic ones,
so a demo definition cannot win by being a longer match.

## Candidate identification

For a resolved quantity with a declared unit, only forms tied to THAT unit count
as an answer: `value unit`, `low-high unit`, `low to high unit`. Everything else
in the sentence is contextual and never range-checked, so a valid value beside a
date and a record count is judged on the value alone.

A date, a mass and a bare number fail for one reason: no candidate satisfies the
registry contract. Nothing here knows what a date or a mass is. Self-checks
refuse the pack if any unit, quantity or industry word appears in the generic
code, or if the guard duplicates anything `GroundingService` owns.

Unitless quantities keep their bounds: a single number in a sentence naming the
parameter is checked, and two numbers with no unit to tell them apart fail
closed rather than range-checking an arbitrary one.

## Certified

17 crafted facts, all green, every code and unit in them invented for the test.

Live, mandatory: the natural casting-speed question. The registry knows the
vocabulary and can vouch for no definition, so the answer presented no date, no
mass, neither synthetic range, and no value carrying any registry unit. That is
the pass. No data was altered to produce a number.

## Open, and not fixed here

- The optional approved-definition live check could not run: one psql query in
  the runner returns a single character while every other query in the same
  function works. Cause unknown; an earlier explanation blaming the pipe
  character was measured to be wrong. It cannot decide certification and is
  reported as a warning.
- The casting-speed question was answered from unrelated policy documents rather
  than refused. The guard behaved correctly - the answer carried no numbers, so
  there was nothing to block - but retrieval has no relevance floor and always
  returns its best chunks. Calibration belongs to T-076.
