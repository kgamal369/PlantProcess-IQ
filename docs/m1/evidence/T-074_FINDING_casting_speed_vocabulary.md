# Finding: casting-speed registry vocabulary is synthetic-only and ambiguous

**Raised by:** Worker 1 during T-074
**Status:** recorded, outside T-074. Not cleaned up inside the task.
**Measured:** 2026-08-08, `ppiq_presentation`

## The measurement

`parameter_definitions` holds 48 rows: 40 synthetic, 8 approved, 0 deleted.

The only definitions a natural casting-speed question matches are:

| code | name | unit | min | max | synthetic |
|---|---|---|---|---|---|
| `CASTING_SPEED` | Casting Speed | m/min | 0.500000 | 2.500000 | yes |
| `CASTING_SPEED_MPM` | Casting speed | m/min | 0.000000 | 3.000000 | yes |

Approved definitions matching the question: **zero**.

Both normalise to the same strongest phrase, carry the same unit, and declare
**different ranges**. They are therefore ambiguous as well as unapproved, and
merging them would invent a composite definition nobody approved.

## Consequence

An engineer asking about casting speed receives no registry-validated answer.
That is correct behaviour and a valid T-074 outcome, but the casting-speed story
currently rests on demo vocabulary alone.

## Not claimed

No root cause, and no recommendation about which definition should become
authoritative. Whether a configured casting-speed parameter should exist for the
presentation is a product decision, not Worker 1's.
