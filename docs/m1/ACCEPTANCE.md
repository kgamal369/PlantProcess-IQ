# M1 ACCEPTANCE - THE UI/UX GOLDEN GATE

**Task:** T-005 | **Milestone:** M1 | **Phase:** M1-P1

A screen is not Green until every line below is ticked **with an evidence file
name beside it**. A tick without evidence is an opinion.

Generate the per-screen instances with:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\m1\New-ScreenChecklist.ps1

## The gate

| # | Line | How it is evidenced |
|---|---|---|
| G01 | `Standard*` component used wherever one exists | Architecture suite green plus a screenshot |
| G02 | No raw local styling where a token or class exists | `uiConformanceRatchet` at or below baseline |
| G03 | Primary action in Electric Blue | Screenshot |
| G04 | Selection in Electric Cyan or Cyan Green | Screenshot with a selection applied |
| G05 | Secondary action in Corporate Blue | Screenshot |
| G06 | Warning and refusal in Amber | Screenshot of a refusal, with its sentence |
| G07 | Destructive and error in Hot Red | Screenshot of a failure state |
| G08 | Muted, inactive and excluded in Muted Steel | Screenshot of an excluded value |
| G09 | `inline-start` and `inline-end` only, never left or right | Grep of the stylesheet plus an RTL screenshot |
| G10 | A complete keyboard path through the screen | Screen capture driven by keyboard only |
| G11 | RTL mirrors correctly | Screenshot with `dir="rtl"` |
| G12 | State: Empty | Screenshot |
| G13 | State: Loading | Screenshot |
| G14 | State: Populated | Screenshot |
| G15 | State: Filtered-empty, worded differently from Empty | Screenshot of both, side by side |
| G16 | State: Blocked, with the measured value beside its threshold | Screenshot of the readiness reason |
| G17 | State: Refused, with a sentence | Screenshot. A red outline with no sentence beside it is a specification failure |
| G18 | State: Failed, distinct from Refused | Screenshot |
| G19 | One widget failing does not kill the page | Failure injection capture |
| G20 | Customer vocabulary is registry-driven | Add a registry row, confirm it appears with no code change |
| G21 | No plant vocabulary compiled into product logic | Architecture test green |
| G22 | No number without resolvable evidence wherever intelligence is claimed | Click through from a figure to its provenance |
| G23 | No internal token in any customer-visible string | PPIQ-T12 green |
| G24 | Terminology matches the Chapter 3 page name | Side-by-side of the screen title and the Chapter 3 contract |

## The three states people get wrong

**Empty is not Filtered-empty.** Empty means there is no data. Filtered-empty
means the selection returned nothing. A customer who sees "no data" after
clicking a filter concludes the product is broken.

**Blocked is not Failed.** Blocked is the readiness gate refusing on purpose,
with a measured value beside its threshold, in Amber. Failed is something
breaking, in Hot Red. Rendering a Blocked state in red turns the strongest
honesty feature in the product into an error message.

**Refused always carries a sentence.** A red outline with nothing beside it is
a failure of the specification, not of the user.

## Evidence

Everything goes in `docs/m1/evidence/`, named `<screenId>_<gate>_<what>.png`
or `.txt`. Example: `S12_G16_readiness_blocked.png`.

Generated tool logs are not committed - `docs/m1/evidence/_gate_logs/` is in
`.gitignore`, and every artifact in this folder is pure ASCII or an image.