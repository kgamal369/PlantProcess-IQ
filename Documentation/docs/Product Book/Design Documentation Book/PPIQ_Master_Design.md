# PlantProcess IQ — Master Design v4.10.2 Revision Record

**Date:** 4 September 2026  
**Type:** second consistency correction / catalogue-certification hardening; no new product capability and no release redefinition.

## Authority rule

PlantProcess IQ still has exactly six design-authority chapters. This revision changes **Chapter 3 only**. Chapters 1, 2, 4, 5 and 6 remain unchanged from v4.10. The revision record is not a seventh design chapter.

## Supersedes

- Master Design Chapter 3 v4.10.1 is superseded by `PPIQ_Chapter3_General_Technical_Function_Description_v4_10_2.md`.
- v4.10.1 remains historical evidence of the first physical-catalogue correction.

## Integrated corrections after Worker-1 review

1. `GENERATED_INFERENCE` is **draft-only** for lifecycle/family/owner/design-clause classification and cannot satisfy the fresh-install zero-unknown gate.
2. Canonical plant fact families (`CANONICAL_STRUCTURE`, `CANONICAL_PROCESS`, `QUALITY_DOWNTIME`) are permanent customer-truth authorities unless an owning design clause explicitly says otherwise; `_events` is not a logging classification.
3. The physical catalogue distinguishes **origin/source-declared schema**, **governed target/effective schema**, and **live-observed schema**. Product tables live in `public` only as historical source declarations during bounded convergence, never as fresh-runtime authority.
4. Table purpose extraction must return a semantic sentence. Divider banners/file headers are not `SOURCE_EXPLICIT` purposes and become `REVIEW_REQUIRED`.
5. New physical table/view names may not use terminal generation suffixes such as `_v1`/`_v2`; version belongs in row-level version authority and migration history. Existing names are grandfathered pending owned convergence.

## Explicit non-changes

- No mass rename or delete is authorised.
- Empty legacy namespace shells remain permitted when they contain zero governed product base tables.
- `ppiq_presentation` remains historical evidence, never M2 generic-product authority.
- M2/M3 dates and capability scopes are unchanged.
- The source-generated catalogue remains subordinate to executable schema/design authority and must be reconciled with live `ppiq_app` and fresh `ppiq_acceptance_empty` before cleanup decisions.
