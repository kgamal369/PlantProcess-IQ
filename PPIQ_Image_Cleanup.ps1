<#
====================================================================================================
 PlantProcess IQ - Docker image audit + safe cleanup   (pure ASCII; runs as a .ps1 file on PS 5.1)
====================================================================================================
 Removes ORPHANED / DUPLICATE / UNUSED images so your environment has one clean set. It NEVER removes
 an image that any container (running or stopped) references, and it only targets a known orphan list.

 DRY RUN by default - shows what it WOULD remove. Add  -Apply  to actually remove.

   .\PPIQ_Image_Cleanup.ps1            # audit only (safe, shows the plan)
   .\PPIQ_Image_Cleanup.ps1 -Apply     # perform the removals

 This does NOT pull anything (all operations are local), so a broken registry/DNS does not matter.
====================================================================================================
#>
param([switch]$Apply)

& {
  $ErrorActionPreference = 'Stop'
  Set-StrictMode -Version 2.0

  function Info([string]$m){ Write-Host $m -ForegroundColor Cyan }
  function Warn([string]$m){ Write-Host $m -ForegroundColor Yellow }
  function Invoke-Docker {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'SilentlyContinue'
    $out = & docker @args 2>$null
    $code = $LASTEXITCODE
    $ErrorActionPreference = $old
    return [pscustomobject]@{ Code = $code; Out = $out }
  }

  if(((Invoke-Docker info).Code -ne 0)){ Warn "Docker is not available - start Docker Desktop and re-run."; return }

  Info "============================================================================"
  Info " PPIQ Docker image cleanup  (mode: $(if($Apply){'APPLY'}else{'DRY RUN'}))"
  Info "============================================================================"

  # Images referenced by ANY container (running or stopped) - these are never touched.
  $usedByContainer = @((Invoke-Docker ps -a --format '{{.Image}}').Out | ForEach-Object { "$_".Trim() } | Where-Object { $_ }) | Sort-Object -Unique

  # The KNOWN orphan set: old build-name schemes and an unused engine tag.
  # (Everything else is left strictly alone.)
  $orphanCandidates = @(
    'plantprocessiq-plantprocess-website:latest',
    'plantprocessiq-plantprocess-workers:latest',
    'plantprocessiq/app-web:local',
    'plantprocessiq/website:local',
    'postgres:17-alpine',
    'mysql:8'
  )

  Info "`nImages referenced by a container (KEPT no matter what):"
  if($usedByContainer.Count -gt 0){ $usedByContainer | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray } } else { Write-Host "    (none)" -ForegroundColor DarkGray }

  $toRemove = @()
  $skipped  = @()
  foreach($img in $orphanCandidates){
    $exists = ((Invoke-Docker image inspect $img).Code -eq 0)
    if(-not $exists){ continue }
    if($usedByContainer -contains $img){ $skipped += $img; continue }
    $toRemove += $img
  }

  Info "`nPlanned removals (orphaned / duplicate / unused, not referenced by any container):"
  if($toRemove.Count -eq 0){ Write-Host "    (nothing to remove - already clean)" -ForegroundColor Green }
  foreach($img in $toRemove){ Write-Host "    - $img" -ForegroundColor Yellow }
  if($skipped.Count -gt 0){
    Warn "`nSkipped (a container still references these - stop/remove the container first if you want them gone):"
    $skipped | ForEach-Object { Write-Host "    ~ $_" -ForegroundColor DarkGray }
  }

  if($Apply -and $toRemove.Count -gt 0){
    Info "`nRemoving..."
    foreach($img in $toRemove){
      $r = Invoke-Docker rmi $img
      if($r.Code -eq 0){ Write-Host "    removed: $img" -ForegroundColor Green }
      else { Warn "    could not remove ${img}:"; $r.Out | Write-Host }
    }
    Info "`nReclaiming dangling layers (safe - dangling only, never tagged images):"
    (Invoke-Docker image prune -f).Out | Write-Host
  } elseif($toRemove.Count -gt 0){
    Warn "`nDRY RUN - nothing removed. Re-run with  -Apply  to perform the removals above."
  }

  Info "`n--- images after (docker images) ---"
  (Invoke-Docker images).Out | Write-Host
  Info "============================================================================"
}
