# scripts/demo

`Rebuild-PresentationDb.ps1` is the ONLY supported way to build or repair the
demo database (`ppiq_presentation`). It is idempotent and takes ~2 minutes:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\demo\Rebuild-PresentationDb.ps1 -Execute
    .\scripts\run\start-api.ps1 -Profile presentation

Input fixture: `deploy/.ppiq-snapshots/ppiq_app_20260713_203359.dump` (29.4 MB,
pre-purge dataset: 40,148 material units / 51,691 quality events / 35,906
genealogy edges / 320 engine findings). The fixture is NOT in git - record its
archive location and keep a copy off-machine.

The demo database is a reproducible artifact, never truth. Three database
purposes, one codebase:

| database              | purpose                                  | profile        |
|-----------------------|------------------------------------------|----------------|
| ppiq_app              | daily development                        | local          |
| ppiq_acceptance_empty | Rule-2 "starts empty" acceptance         | (v23 M2-19)    |
| ppiq_presentation     | populated customer demo                  | presentation   |

The other scripts here are superseded steps kept for reference; prefer the
one-command rebuild.