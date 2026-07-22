<#
.SYNOPSIS
    Fix-CanvasBuildErrors.ps1 (v2) - fixes the 5 tsc -b errors from M2-31/M2-37.
    v2 change: the apiClient import specifier is DISCOVERED from
    src\api\advancedAnalysis.ts (the file that provably imports it in your
    build) instead of assuming a filename - v1 aborted because it tested for a
    literal http.ts while the module may be http\index.ts, http.tsx, or an
    alias. Fixes: (1) apiClient imports, (2) computeCorrelation ->
    runCorrelation, (3) DatasetNode/BlockNode with @xyflow v12 Node<Data>
    generics. Gate: npx tsc -b. Auto-revert.
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-CanvasBuildErrors.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Get-Location).Path, [switch]$NoGate, [switch]$Revert)
$LogName = 'Fix_CanvasBuild'


$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest
$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ($LogName + '_' + $stamp + '.txt')
$lines   = New-Object System.Collections.Generic.List[string]
$utf8    = New-Object System.Text.UTF8Encoding($false)
function W([string]$t = '') { $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n") + "`r`n"), $utf8); Write-Host ''; Write-Host ('Log: ' + $logPath) -ForegroundColor Cyan }
$backups = @{}
function Backup([string]$full) {
    if (-not $backups.ContainsKey($full)) {
        Copy-Item -LiteralPath $full -Destination ($full + '.' + $stamp + '.bak') -Force
        $backups[$full] = $full + '.' + $stamp + '.bak'
    }
}
function Revert-All {
    foreach ($k in $backups.Keys) { Copy-Item -LiteralPath $backups[$k] -Destination $k -Force }
    W '  reverted.'
}


W '=============================================================================='
W ('FIX CANVAS BUILD ERRORS v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
$web = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$src = Join-Path $web 'src'
$fAdv       = Join-Path $src 'api\advancedAnalysis.ts'
$fCanvasApi = Join-Path $src 'api\canvasApi.ts'
$fAssoc     = Join-Path $src 'state\AssociativeContext.tsx'
$fToolbox   = Join-Path $src 'pages\Analysis\AnalysisToolboxPage.tsx'
$fDataset   = Join-Path $src 'canvas\nodes\DatasetNode.tsx'
$fBlock     = Join-Path $src 'canvas\nodes\BlockNode.tsx'
if ($Revert) {
    W '[REVERT]'
    foreach ($f in @($fCanvasApi,$fAssoc,$fToolbox,$fDataset,$fBlock)) {
        $b = Get-ChildItem -Path (Split-Path $f) -Filter ((Split-Path $f -Leaf) + '.*.bak') -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($b) { Copy-Item $b.FullName $f -Force; W ('  restored ' + (Split-Path $f -Leaf)) }
    }
    Save; exit 0
}
W '[PREFLIGHT]'
$fail = $false
foreach ($f in @($fAdv,$fCanvasApi,$fAssoc,$fToolbox,$fDataset,$fBlock)) {
    if (Test-Path -LiteralPath $f) { W ('  found  ' + (Split-Path $f -Leaf)) } else { W ('  MISSING ' + $f); $fail = $true }
}
if ($fail) { Save; exit 2 }

W ''
W '[DISCOVER] apiClient import specifier from advancedAnalysis.ts'
$adv = [System.IO.File]::ReadAllText($fAdv)
$m = [System.Text.RegularExpressions.Regex]::Match($adv, 'import\s*\{\s*apiClient\s*\}\s*from\s*"([^"]+)"')
if (-not $m.Success) { W '  FAILED: advancedAnalysis.ts has no apiClient import - paste its first 10 lines.'; Save; exit 2 }
$spec = $m.Groups[1].Value
W ('  specifier used by the codebase: "' + $spec + '"')
if ($spec.StartsWith('@')) {
    $specApi = $spec        # alias works from anywhere
    $specState = $spec
} elseif ($spec.StartsWith('./')) {
    $specApi = $spec                          # canvasApi.ts sits in the same folder
    $specState = '../api/' + $spec.Substring(2)  # AssociativeContext sits in src\state
} else {
    $specApi = $spec; $specState = $spec
}
W ('  canvasApi.ts will import from:          "' + $specApi + '"')
W ('  AssociativeContext.tsx will import from: "' + $specState + '"')


W ''
W '[FIX 1] apiClient imports'
try {
    $s = [System.IO.File]::ReadAllText($fCanvasApi)
    $newImp = 'import { apiClient } from "' + $specApi + '";'
    if ($s.Contains($newImp)) { W '  canvasApi.ts already fixed' }
    else {
        Backup $fCanvasApi
        $s = $s.Replace('import { apiClient } from "./apiClient";', $newImp)
        [System.IO.File]::WriteAllText($fCanvasApi, $s, $utf8)
        if (-not ([System.IO.File]::ReadAllText($fCanvasApi)).Contains($newImp)) { throw 'canvasApi verify' }
        W '  canvasApi.ts fixed'
    }
    $s = [System.IO.File]::ReadAllText($fAssoc)
    $newImp2 = 'import { apiClient } from "' + $specState + '";'
    if ($s.Contains($newImp2)) { W '  AssociativeContext.tsx already fixed' }
    else {
        Backup $fAssoc
        $s = $s.Replace('import { apiClient } from "../api/apiClient";', $newImp2)
        [System.IO.File]::WriteAllText($fAssoc, $s, $utf8)
        if (-not ([System.IO.File]::ReadAllText($fAssoc)).Contains($newImp2)) { throw 'AssociativeContext verify' }
        W '  AssociativeContext.tsx fixed'
    }
} catch { W ('  FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }

W ''
W '[FIX 2] computeCorrelation -> runCorrelation'
try {
    $s = [System.IO.File]::ReadAllText($fToolbox)
    if ($s.Contains('runCorrelation')) { W '  already fixed' }
    else {
        Backup $fToolbox
        [System.IO.File]::WriteAllText($fToolbox, $s.Replace('computeCorrelation', 'runCorrelation'), $utf8)
        if (-not ([System.IO.File]::ReadAllText($fToolbox)).Contains('runCorrelation')) { throw 'verify' }
        W '  fixed (import + call + parity note)'
    }
} catch { W ('  FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }


W ''
W '[FIX 3] DatasetNode + BlockNode: @xyflow v12 Node<Data> generics'
try {
Backup $fDataset
$cDataset = @'
import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";
import { PORT_COLORS, inferPortType } from "../ports";

export type DatasetColumn = { name: string; sqlType: string; isKeyCandidate?: boolean };
export type DatasetNodeData = {
  table: string; source: string; columns: DatasetColumn[];
  [key: string]: unknown;
};
type DatasetNodeType = Node<DatasetNodeData, "dataset">;

/** A staged table: every column is a typed source+target port (spec S3/S4). */
export function DatasetNode({ data, selected }: NodeProps<DatasetNodeType>) {
  return (
    <div className={"ds-node" + (selected ? " selected" : "")}>
      <div className="ds-node__head">
        <span className="ds-node__name">{data.table}</span>
        <span className="ds-node__src">{data.source}</span>
      </div>
      {data.columns.map((c) => {
        const pt = c.isKeyCandidate ? "key" : inferPortType(c.sqlType);
        const color = PORT_COLORS[pt];
        return (
          <div className="ds-node__col" key={c.name}>
            <Handle id={"in:" + c.name} type="target" position={Position.Left} style={{ background: color }} />
            <span>{c.name}</span>
            <span className="t">{c.sqlType}</span>
            <Handle id={"out:" + c.name} type="source" position={Position.Right} style={{ background: color }} />
          </div>
        );
      })}
    </div>
  );
}
'@
[System.IO.File]::WriteAllText($fDataset, $cDataset, $utf8)
if (-not ([System.IO.File]::ReadAllText($fDataset)).Contains('NodeProps<DatasetNodeType>')) { throw 'DatasetNode verify' }
W '  DatasetNode.tsx rewritten (verified)'
Backup $fBlock
$cBlock = @'
import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";

export type BlockField = {
  key: string; label: string; options?: string[]; value: string; type?: "select" | "number";
};
export type BlockNodeData = {
  kind: string; title: string;
  fields?: BlockField[];
  onField?: (nodeId: string, key: string, value: string) => void;
  hasIn?: boolean; hasOut?: boolean;
  [key: string]: unknown;
};
type BlockNodeType = Node<BlockNodeData, "block">;

/** Generic toolbox block (spec S7): typed flow ports + inline config. */
export function BlockNode({ id, data }: NodeProps<BlockNodeType>) {
  return (
    <div className="blk-node">
      {data.hasIn !== false && <Handle type="target" position={Position.Left} style={{ background: "#2ce6a2" }} />}
      <div className="blk-node__kind">{data.kind}</div>
      <div className="blk-node__title">{data.title}</div>
      {(data.fields ?? []).map((f) =>
        f.type === "number" ? (
          <input key={f.key} type="number" value={f.value} aria-label={f.label}
            onChange={(e) => data.onField?.(id, f.key, e.target.value)} />
        ) : (
          <select key={f.key} value={f.value} aria-label={f.label}
            onChange={(e) => data.onField?.(id, f.key, e.target.value)}>
            {(f.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}
          </select>
        )
      )}
      {data.hasOut !== false && <Handle type="source" position={Position.Right} style={{ background: "#2ce6a2" }} />}
    </div>
  );
}
'@
[System.IO.File]::WriteAllText($fBlock, $cBlock, $utf8)
if (-not ([System.IO.File]::ReadAllText($fBlock)).Contains('NodeProps<BlockNodeType>')) { throw 'BlockNode verify' }
W '  BlockNode.tsx rewritten (verified)'
} catch { W ('  FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }

if (-not $NoGate) {
    W ''
    W '[GATE] npx tsc -b (auto-revert on failure)'
    Push-Location $web
    $o = & npx tsc -b 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    foreach ($l in ($o | Select-Object -Last 15)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  tsc -b FAILED - reverting all fixes.'; Revert-All; Save; exit 1 }
    W '  tsc -b GREEN'
}


W ''
W 'DONE. Then:'
W '  1. STOP the running API (it holds PlantProcess.Api.dll).'
W '  2. Re-run: powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-M238ChartCatalogue.ps1'
W '  3. npm run build in Frontend\PlantProcess.Web -> expect zero errors.'
W '  4. Restart the API (pareto validation + canvas endpoints).'
W ('Revert anytime: powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-CanvasBuildErrors.ps1 -Revert')


Save
exit 0