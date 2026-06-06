# PlantProcess IQ Phase 7 + Phase 8 Evidence

Generated: 2026-06-06T08:48:56.857Z

## P07 — Internationalization + Arabic RTL

- Locale runtime added with persistent key `plantprocess.locale.v1`.
- English and Arabic bundles added.
- Document `lang` and `dir` are driven by locale.
- RTL/mobile CSS hardening and 44px tap target rule added.
- `/i18n-rtl` readiness route and Playwright matrix spec added.

## P08 — Backend God-File Refactor & API Hygiene

- Backend god-file inventory and hygiene report generated.
- Source-route contract snapshot generated before destructive endpoint refactoring.
- Backend hygiene gate blocks unknown new oversized API/Application files.

## Boundary

This pack does not destructively split the largest backend endpoint files yet. It creates the route-contract and file-size guard needed to split them safely one target at a time.
