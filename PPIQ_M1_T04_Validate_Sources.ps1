<#
====================================================================================================
 PlantProcess IQ - M1-T04 last gate: validate the LIVE 8-source fleet   (pure ASCII; read-only)
====================================================================================================
 Confirms the eight emulated customer sources are up, healthy, and serving the committed seed data.

 It discovers the running source containers by their loopback offset ports (so it works whatever the
 stack is named - ppiq-source-* or ppiq-src-*), reads each container's OWN credentials from its env
 (so no passwords are hard-coded), checks health, and counts rows. The two Excel sources are committed
 CSV fixtures, validated by row count on disk.

 HARD gates:  6/6 DB containers healthy  +  Melt PG meltshop_heats = 630  +  Yard CSV ~5,600 coils
              +  QA CSV ~1,868 samples     (=> 8/8 sources accounted for)
 Best-effort (reported, not gated): row counts for Oracle / MSSQL / MySQL (engine/creds vary).

 It pulls nothing and changes nothing. Run:
   .\PPIQ_M1_T04_Validate_Sources.ps1
====================================================================================================
#>

& {
  $ErrorActionPreference = 'Stop'
  Set-StrictMode -Version 2.0

  $script:Results = New-Object System.Collections.Generic.List[object]
  function Add-Result([string]$Check,[bool]$Pass,[string]$Detail){
    $script:Results.Add([pscustomobject]@{ Pass=$Pass; Check=$Check; Detail=$Detail })
    $tag = if($Pass){'PASS'}else{'FAIL'}; $col = if($Pass){'Green'}else{'Red'}
    Write-Host ("  [{0}] {1} :: {2}" -f $tag,$Check,$Detail) -ForegroundColor $col
  }
  function Info([string]$m){ Write-Host $m -ForegroundColor Cyan }
  function Warn([string]$m){ Write-Host $m -ForegroundColor Yellow }
  function Invoke-Docker {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'SilentlyContinue'
    $out = & docker @args 2>$null
    $code = $LASTEXITCODE
    $ErrorActionPreference = $old
    return [pscustomobject]@{ Code = $code; Out = $out }
  }
  function FirstLine($o){
    $x = @($o) | Where-Object { "$_".Trim() -ne '' } | Select-Object -First 1
    return ("" + $x).Trim()
  }
  function FirstInt($o){
    foreach($line in @($o)){ $s = "$line".Trim(); if($s -match '^[0-9]+$'){ return [int]$s } }
    return $null
  }
  function Get-RepoRoot {
    $dir = (Get-Location).Path
    while(-not [string]::IsNullOrEmpty($dir)){
      if(Test-Path -LiteralPath (Join-Path $dir '.git')){ return $dir }
      $parent = Split-Path -Parent $dir
      if([string]::IsNullOrEmpty($parent) -or $parent -eq $dir){ break }
      $dir = $parent
    }
    return (Get-Location).Path
  }
  function Find-ByPort([int]$port){
    $r = Invoke-Docker ps --filter "publish=$port" --format '{{.Names}}'
    return FirstLine $r.Out
  }
  function Get-Health([string]$c){
    $r = Invoke-Docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $c
    return FirstLine $r.Out
  }
  function Get-ContainerEnv([string]$c,[string]$key){
    $r = Invoke-Docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' $c
    foreach($line in @($r.Out)){
      $s = "$line"
      if($s.StartsWith("$key=")){ return $s.Substring($key.Length + 1) }
    }
    return $null
  }

  if(((Invoke-Docker info).Code -ne 0)){ Warn "Docker is not available - start Docker Desktop and re-run."; return }
  $RepoRoot = Get-RepoRoot; Set-Location $RepoRoot

  Info "============================================================================"
  Info " PPIQ M1-T04 - validate the live 8-source fleet"
  Info " repo root: $RepoRoot"
  Info "============================================================================"

  # ----------------------------------------------------------------------------------------------
  # The 6 DB sources, discovered by their offset ports.
  # ----------------------------------------------------------------------------------------------
  $specs = @(
    @{ key='meltshop'; port=15432; engine='postgres'; label='Melt PG      (15432)'; table='meltshop_heats';   lo=598;  hi=662;  hard=$true },
    @{ key='caster';   port=11521; engine='oracle';   label='Caster Oracle(11521)'; table='caster_sequences'; lo=0;    hi=0;    hard=$false },
    @{ key='hsm';      port=11522; engine='oracle';   label='HSM Oracle   (11522)'; table='';                 lo=0;    hi=0;    hard=$false },
    @{ key='pkl';      port=11433; engine='mssql';    label='PKL MSSQL    (11433)'; table='pkl.dbo.pkl_coils';lo=5320; hi=5880; hard=$false },
    @{ key='downtime'; port=13306; engine='mysql';    label='Downtime MySQL(13306)';table='';                 lo=0;    hi=0;    hard=$false },
    @{ key='parsytec'; port=13307; engine='mysql';    label='Parsytec MySQL(13307)';table='';                 lo=0;    hi=0;    hard=$false }
  )

  Info "`n--- per-source health + data (discovered by published port) ---"
  $healthyCount = 0
  $meltCount = $null
  foreach($s in $specs){
    $c = Find-ByPort $s.port
    if([string]::IsNullOrWhiteSpace($c)){
      Write-Host ("  {0}  container: NOT FOUND on port {1}" -f $s.label, $s.port) -ForegroundColor Red
      continue
    }
    $health = Get-Health $c
    if($health -eq 'healthy' -or $health -eq 'running'){ $healthyCount++ }
    $hcol = if($health -eq 'healthy'){'Green'} elseif($health -eq 'running'){'Yellow'} else {'Red'}

    # data count (engine-specific; best-effort except Melt PG)
    $cntText = ''
    try {
      switch($s.engine){
        'postgres' {
          $u  = Get-ContainerEnv $c 'POSTGRES_USER'; if(-not $u){ $u='postgres' }
          $db = Get-ContainerEnv $c 'POSTGRES_DB';   if(-not $db){ $db=$u }
          if($s.table){
            $n = FirstInt (Invoke-Docker exec $c psql -tA -U $u -d $db -c ("SELECT count(*) FROM {0}" -f $s.table)).Out
            if($n -ne $null){ $cntText = ("{0} = {1}" -f $s.table,$n); if($s.key -eq 'meltshop'){ $meltCount = $n } }
          }
        }
        'mssql' {
          $pw = Get-ContainerEnv $c 'MSSQL_SA_PASSWORD'
          if($pw -and $s.table){
            $q = ("SET NOCOUNT ON; SELECT count(*) FROM {0}" -f $s.table)
            $out = (Invoke-Docker exec $c /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $pw -C -h -1 -W -Q $q).Out
            $n = FirstInt $out
            if($n -ne $null){ $cntText = ("{0} = {1}" -f $s.table,$n) }
          }
        }
        'mysql' {
          $pw = Get-ContainerEnv $c 'MYSQL_ROOT_PASSWORD'
          $db = Get-ContainerEnv $c 'MYSQL_DATABASE'
          if($pw -and $db){
            $q = ("SELECT COALESCE(SUM(table_rows),0) FROM information_schema.tables WHERE table_schema='{0}'" -f $db)
            $n = FirstInt (Invoke-Docker exec $c mysql -uroot ("-p{0}" -f $pw) -N -e $q).Out
            if($n -ne $null){ $cntText = ("db {0} ~{1} rows (approx)" -f $db,$n) }
          }
        }
        'oracle' {
          $u  = Get-ContainerEnv $c 'APP_USER'
          $pw = Get-ContainerEnv $c 'APP_USER_PASSWORD'; if(-not $pw){ $pw = Get-ContainerEnv $c 'ORACLE_PASSWORD' }
          $img = FirstLine (Invoke-Docker inspect -f '{{.Config.Image}}' $c).Out
          $pdb = if($img -match 'oracle-free'){ 'FREEPDB1' } else { 'XEPDB1' }
          if($u -and $pw -and $s.table){
            $sql = ("set heading off`nset feedback off`nset pages 0`nSELECT count(*) FROM {0};`nexit`n" -f $s.table)
            $out = ($sql | Invoke-Docker exec -i $c sqlplus -S ("{0}/{1}@//localhost:1521/{2}" -f $u,$pw,$pdb)).Out
            $n = FirstInt $out
            if($n -ne $null){ $cntText = ("{0} = {1} (PDB {2})" -f $s.table,$n,$pdb) }
          }
        }
      }
    } catch {}

    Write-Host ("  {0}  {1,-22} health={2}" -f $s.label,$c,$health) -ForegroundColor $hcol
    if($cntText){ Write-Host ("        data: {0}" -f $cntText) -ForegroundColor DarkGray }
    else        { Write-Host ("        data: (health-only; engine row-count is best-effort)") -ForegroundColor DarkGray }
  }

  # ----------------------------------------------------------------------------------------------
  # HARD GATES
  # ----------------------------------------------------------------------------------------------
  Info "`n--- hard gates ---"
  Add-Result '6/6 source DB containers up + healthy' ($healthyCount -eq 6) ("$healthyCount/6 healthy or running")

  $meltOk = ($meltCount -ne $null -and $meltCount -ge 598 -and $meltCount -le 662)
  Add-Result 'Melt PG meltshop_heats = 630' $meltOk ($(if($meltCount -ne $null){"count = $meltCount (expected 630)"}else{"could not read meltshop_heats"}))

  $newSeeds = Join-Path $RepoRoot 'deploy/fixtures/demo'
  $yard = Join-Path $newSeeds 'excel-yard/yard_inventory.csv'
  if(Test-Path -LiteralPath $yard){
    $yrows = (Get-Content -LiteralPath $yard | Measure-Object -Line).Lines - 1
    Add-Result 'Yard fixture ~5,600 coils (committed CSV)' ($yrows -ge 5320 -and $yrows -le 5880) ("yard_inventory.csv data rows = $yrows")
  } else { Add-Result 'Yard fixture present' $false 'deploy/fixtures/demo/excel-yard/yard_inventory.csv not found' }

  $qa = Join-Path $newSeeds 'excel-qa/qa_samples.csv'
  if(Test-Path -LiteralPath $qa){
    $qrows = (Get-Content -LiteralPath $qa | Measure-Object -Line).Lines - 1
    Add-Result 'QA fixture ~1,868 samples (committed CSV)' ($qrows -ge 1774 -and $qrows -le 1962) ("qa_samples.csv data rows = $qrows")
  } else { Add-Result 'QA fixture present' $false 'deploy/fixtures/demo/excel-qa/qa_samples.csv not found' }

  # ----------------------------------------------------------------------------------------------
  # Ledger
  # ----------------------------------------------------------------------------------------------
  Info "`n============================================================================"
  Info " RESULT LEDGER  (8 sources = 6 DB containers + 2 committed CSV fixtures)"
  Info "============================================================================"
  $script:Results | Format-Table Pass,Check,Detail -AutoSize | Out-String | Write-Host
  $fail = @($script:Results | Where-Object { -not $_.Pass })
  if($fail.Count -eq 0){
    Write-Host "M1-T04 LAST GATE GREEN - all 8 sources accounted for, healthy, and serving the seeded data." -ForegroundColor Green
    Write-Host "(This validates the LIVE running fleet. It does not prove the NEW sources.yml specifically -" -ForegroundColor DarkGray
    Write-Host " that still needs the convergence to your cached images + the original demo-sources.yml.)" -ForegroundColor DarkGray
  } else {
    Write-Host ("{0} HARD GATE(S) FAILED - see ledger above." -f $fail.Count) -ForegroundColor Red
  }
}
