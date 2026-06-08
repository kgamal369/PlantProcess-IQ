
# T-050 AI Regression Sweep

Marker: PPIQ_REALIZATION_T050_PHASE9_AI_REGRESSION_SWEEP

## Purpose

Close Phase 09 by proving that suggestions, assistant grounding, private model gateway controls, and assistant demo behavior remain green together.

## Acceptance

- T-047 suggestion workflow green.
- T-048 assistant grounding eval gate green.
- T-049 model gateway serving modes green.
- Assistant answers approved demo question with citation.
- Assistant blocks invented number.
- Assistant blocks unsupported causal/value overclaim.
- Self-hosted no-egress model mode can feed grounded assistant answer.
- Phase 09 rollup report generated.

## Validation

Run:

    node tools/phase9/validate-t050-ai-regression-sweep.cjs
    scripts/phase9/validate-phase9-rollup.ps1
