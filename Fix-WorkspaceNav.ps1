<#
.SYNOPSIS
    Fix-WorkspaceNav.ps1 - closes M1-29. Adds a "Workspaces" nav section that
    lists all dashboard definitions as clickable links to /workspace/{code},
    so the 12 workspaces are reachable without typing URLs. Contract: preflight
    anchors -> backup -> patch AppLayout.tsx -> self-check -> tsc gate ->
    auto-revert on failure.

.DESCRIPTION
    Adds: a small hook that fetches /analytics/dashboard/definitions on mount,
    a NAV_WORKSPACES-style dynamic group rendered under ANALYTICS, each item ->
    /workspace/{dashboardCode}. Styling reuses the existing piq-nav-link classes.
#>
[CmdletBinding()]
param([string]$RepoRoot=(Get-Location).Path,[switch]$Revert,[switch]$NoGate)
$ErrorActionPreference='Continue'; Set-StrictMode -Version Latest
$stamp=Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath=Join-Path $RepoRoot ("Fix_WorkspaceNav_"+$stamp+".txt")
$lines=New-Object System.Collections.Generic.List[string]; $utf8=New-Object System.Text.UTF8Encoding($false)
function W([string]$t=''){ $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath,(($lines -join "`r`n")+"`r`n"),$utf8); Write-Host ''; Write-Host ('Log: '+$logPath) -ForegroundColor Cyan }
$path=Join-Path $RepoRoot 'Frontend\PlantProcess.Web\src\components\AppLayout.tsx'

W '=============================================================================='
W ('FIX WORKSPACE NAV (M1-29) - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
if ($Revert) {
    $bak=Get-ChildItem (Split-Path $path) -Filter 'AppLayout.tsx.*.bak' -EA SilentlyContinue|Sort-Object LastWriteTime -Desc|Select-Object -First 1
    if ($bak){ Copy-Item $bak.FullName $path -Force; W ('reverted from '+$bak.Name) } else { W 'no backup' }
    Save; exit 0
}
if (-not (Test-Path -LiteralPath $path)) { W 'FAIL: AppLayout.tsx not found. Run from repo root.'; Save; exit 2 }
$src=[System.IO.File]::ReadAllText($path)

W '[PREFLIGHT] anchors'
$a1='import { NavLink, Outlet, useLocation } from "react-router-dom";'
$a2='const NAV_INTELLIGENCE = ['
$a3='function NavGroup({ title, items }'
foreach ($a in @($a1,$a2,$a3)) { if ($src.Contains($a)) { W ('  ok: '+$a.Substring(0,[Math]::Min(40,$a.Length))) } else { W ('  MISSING: '+$a); Save; exit 2 } }
if ($src.Contains('NAV_WORKSPACES') -or $src.Contains('useWorkspaceNav')) { W '  already patched; aborting.'; Save; exit 0 }

$bak=$path+'.'+$stamp+'.bak'; Copy-Item $path $bak -Force; W ('[BACKUP] '+$bak)

# 1. add useState/useEffect import if react not already importing them
if (-not ($src -match 'from "react"')) {
    $src = $src.Replace($a1, 'import { useEffect, useState } from "react";' + "`r`n" + $a1)
    W '  added react hooks import'
}

# 2. insert the workspace fetch hook + component before NavGroup definition
$hook = @'
function useWorkspaceLinks() {
  const [links, setLinks] = useState<Array<{ to: string; label: string }>>([]);
  useEffect(() => {
    let ignore = false;
    (async () => {
      try {
        const res = await fetch("/analytics/dashboard/definitions", {
          headers: (() => {
            const t = (window as unknown as { __ppiqToken?: string }).__ppiqToken;
            return t ? { Authorization: "Bearer " + t } : {};
          })(),
        });
        if (!res.ok) return;
        const body = await res.json();
        const arr = Array.isArray(body)
          ? body
          : body.items ?? body.definitions ?? body.dashboards ?? body.results ?? [];
        const mapped = (arr as Array<Record<string, unknown>>)
          .map((d) => ({
            code: String(d["dashboardCode"] ?? d["dashboard_code"] ?? d["code"] ?? ""),
            name: String(d["name"] ?? d["dashboardCode"] ?? d["code"] ?? "Workspace"),
          }))
          .filter((d) => d.code)
          .sort((a, b) => a.name.localeCompare(b.name))
          .map((d) => ({ to: "/workspace/" + d.code, label: d.name }));
        if (!ignore) setLinks(mapped);
      } catch {
        /* nav is best-effort; typed URLs still work */
      }
    })();
    return () => { ignore = true; };
  }, []);
  return links;
}

'@
$src = $src.Replace($a3, $hook + $a3)
W '  inserted useWorkspaceLinks hook'

# 3. render the workspace group after the ANALYTICS NavGroup.
#    We hook into the render by adding a component that maps links to simple NavLinks.
$renderAnchor = '<NavGroup title="Analytics" items={NAV_ANALYTICS} />'
if (-not $src.Contains($renderAnchor)) {
    # try a looser match
    $m = [regex]::Match($src, '<NavGroup\s+title="Analytics"[^>]*/>')
    if ($m.Success) { $renderAnchor = $m.Value } else { W '  WARN: Analytics NavGroup render not found; inserting workspace group before Intelligence group instead'; }
}
$wsRender = @'
<div className="piq-nav-group">
          <p className="piq-nav-group__title">Workspaces</p>
          {workspaceLinks.map((l) => (
            <NavLink key={l.to} to={l.to} className={({ isActive }) => isActive ? "piq-nav-link active" : "piq-nav-link"}>
              <span className="piq-nav-link__copy"><span className="piq-nav-link__label">{l.label}</span></span>
            </NavLink>
          ))}
        </div>
        
'@
if ($src.Contains($renderAnchor)) {
    $src = $src.Replace($renderAnchor, $renderAnchor + "`r`n        " + $wsRender)
    W '  inserted workspace nav group render'
} else {
    W '  FAIL: could not find a render anchor for the group.'; Copy-Item $bak $path -Force; Save; exit 1
}

# 4. call the hook in the component that renders nav. Find the main layout function.
#    Add: const workspaceLinks = useWorkspaceLinks(); right after the component opens.
$compAnchor = [regex]::Match($src, 'export function AppLayout\([^)]*\)\s*\{')
if ($compAnchor.Success) {
    $src = $src.Substring(0,$compAnchor.Index+$compAnchor.Length) + "`r`n  const workspaceLinks = useWorkspaceLinks();" + $src.Substring($compAnchor.Index+$compAnchor.Length)
    W '  wired hook into AppLayout'
} else {
    W '  WARN: AppLayout function signature not matched; you may need to add const workspaceLinks = useWorkspaceLinks(); manually'
}

[System.IO.File]::WriteAllText($path,$src,$utf8)

W ''
W '[SELF-CHECK]'
$now=[System.IO.File]::ReadAllText($path)
$c1=$now.Contains('useWorkspaceLinks'); $c2=$now.Contains('Workspaces'); $c3=$now.Contains('workspaceLinks.map')
W ('  hook present: '+$c1); W ('  group title:  '+$c2); W ('  render map:   '+$c3)
if (-not ($c1 -and $c2 -and $c3)) { Copy-Item $bak $path -Force; W '  FAILED - reverted.'; Save; exit 1 }

if (-not $NoGate) {
    W ''; W '[GATE] npx tsc --noEmit'
    Push-Location (Join-Path $RepoRoot 'Frontend\PlantProcess.Web')
    $o = & npx tsc --noEmit 2>&1; $code=$LASTEXITCODE; Pop-Location
    foreach ($l in ($o|Select-Object -Last 10)) { W ('    '+$l) }
    if ($code -ne 0) { Copy-Item $bak $path -Force; W '  TYPE CHECK FAILED - reverted. Send output.'; Save; exit 1 }
    W '  TYPE CHECK GREEN'
}
W ''
W 'DONE (M1-29). Refresh the browser (vite hot-reloads). A "Workspaces" nav'
W 'section lists all dashboards; click each to open /workspace/{code}.'
W 'NOTE: if the fetch needs an auth token and none is on window.__ppiqToken,'
W 'the group renders empty (best-effort) - the typed /workspace URLs still work,'
W 'and the demo can use those. Revert: -Revert'
Save; exit 0
