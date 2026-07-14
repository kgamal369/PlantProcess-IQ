#requires -Version 5.1
<#
 PPIQ FIX - StandardTable prop types on the two new pages (tsc -b build errors)
 RUN: powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-StandardTableProps.ps1
      ... -Revert
 Fixes:
   AlertingPage.tsx      loading={...}      -> isLoading={...}   (real prop name)
   AuthorMappingPage.tsx getRowKey r.idx    -> String(r.idx)     (must return string)
 Gate: tsc -b (the real build tsc, not --noEmit) must pass for these files.
#>
param([switch]$Revert)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
function Ok($m){Write-Host "[+] $m" -ForegroundColor Green}; function Er($m){Write-Host "[x] $m" -ForegroundColor Red}; function Inf($m){Write-Host "[i] $m" -ForegroundColor Cyan}
$U=New-Object System.Text.UTF8Encoding($false)
function WT($p,$c){[System.IO.File]::WriteAllText($p,($c -replace "`r`n","`n" -replace "`n","`r`n"),$U)}
function RT($p){[System.IO.File]::ReadAllText($p)}

$Repo=(Get-Location).Path
$Web=Join-Path $Repo 'Frontend\PlantProcess.Web'
$Al=Join-Path $Web 'src\pages\DataIntegration\AlertingPage.tsx'
$Am=Join-Path $Web 'src\pages\DataIntegration\AuthorMappingPage.tsx'
$BR=Join-Path $Repo 'deploy\.ppiq-backups'; $St=Get-Date -Format 'yyyyMMdd_HHmmss'; $BD=Join-Path $BR ("STPROPS_"+$St)

if($Revert){
  $last=Get-ChildItem $BR -Directory -Filter 'STPROPS_*' -ErrorAction SilentlyContinue|Sort-Object Name -Descending|Select-Object -First 1
  if(-not $last){Er "no backup";exit 1}
  Get-ChildItem $last.FullName -Filter '*.bak'|ForEach-Object{
    $o=((Get-Content $_.FullName -TotalCount 1) -replace '^// PPIQ-ORIGINAL-PATH: ','')
    $b=(Get-Content $_.FullName -Raw) -replace "^// PPIQ-ORIGINAL-PATH: [^\r\n]*\r?\n",''
    WT $o $b; Ok "restored $o"
  }; exit 0
}

foreach($f in @($Al,$Am)){ if(-not(Test-Path $f)){Er "missing $f";exit 1} }
New-Item -ItemType Directory -Force -Path $BD|Out-Null
foreach($f in @($Al,$Am)){ [System.IO.File]::WriteAllText((Join-Path $BD ([System.IO.Path]::GetFileName($f)+'.bak')),"// PPIQ-ORIGINAL-PATH: $f`r`n"+(RT $f),$U) }
Ok "backup -> $BD"

# 1. AlertingPage: loading -> isLoading  (rules table). The log StandardTable has no loading prop.
$a=RT $Al
$old1="        data={rules}`r`n        getRowKey={(r) => r.id}`r`n        loading={isLoading}"
$new1="        data={rules}`r`n        getRowKey={(r) => r.id}`r`n        isLoading={isLoading}"
if($a.Contains("loading={isLoading}")){ $a=$a.Replace("loading={isLoading}","isLoading={isLoading}"); WT $Al $a; Ok "AlertingPage: loading -> isLoading" }
else { Er "AlertingPage: 'loading={isLoading}' not found"; & $PSCommandPath -Revert|Out-Null; exit 1 }

# 2. AuthorMappingPage: getRowKey must return string
$m=RT $Am
if($m.Contains("getRowKey={(r) => r.idx}")){ $m=$m.Replace("getRowKey={(r) => r.idx}","getRowKey={(r) => String(r.idx)}"); WT $Am $m; Ok "AuthorMappingPage: getRowKey -> String(r.idx)" }
else { Er "AuthorMappingPage: 'getRowKey={(r) => r.idx}' not found"; & $PSCommandPath -Revert|Out-Null; exit 1 }

# gate: real build tsc
Inf "Gate: tsc -b (real build) ..."
Push-Location $Web
$se=$ErrorActionPreference; $ErrorActionPreference='Continue'
$out = & npx --no-install tsc -b 2>&1
$code=$LASTEXITCODE
$ErrorActionPreference=$se
Pop-Location
$mine=@($out|Select-String -Pattern 'AlertingPage|AuthorMappingPage')
if($mine.Count -gt 0 -or $code -ne 0){
  if($mine.Count -gt 0){
    Er "tsc -b still reports errors in these files - reverting:"
    $mine|ForEach-Object{ Er ("   "+$_.Line) }
    & $PSCommandPath -Revert|Out-Null; exit 1
  } else {
    Inf "tsc -b exit $code but no errors in the two fixed files (other pre-existing project errors may exist)."
    $out | Select-Object -Last 8 | ForEach-Object { Write-Host "   $_" }
  }
}
Ok "The two StandardTable prop errors are fixed."
Inf "Re-run: npm run build   (should pass these two; report any remaining error)."
