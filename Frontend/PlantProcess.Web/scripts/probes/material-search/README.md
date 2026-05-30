# Phase 4 Material Search Runtime Probes

These probes validate the four Material Search actions against the current backend contracts.

| Action | Probe |
|---|---|
| Search | 01-search-materials.ps1 |
| Load Investigation | 02-load-investigation.ps1 |
| Calculate Risk | 03-calculate-risk.ps1 |
| PDF Report | 04-pdf-report.ps1 |

Run after backend is up and authenticated token is available if required:

```powershell
$env:PPIQ_TOKEN = "<jwt-if-needed>"
.\scripts\probes\material-search\01-search-materials.ps1 -Query ""
.\scripts\probes\material-search\02-load-investigation.ps1 -MaterialUnitId "<guid>"
.\scripts\probes\material-search\03-calculate-risk.ps1 -MaterialUnitId "<guid>"
.\scripts\probes\material-search\04-pdf-report.ps1 -MaterialUnitId "<guid>"
```
