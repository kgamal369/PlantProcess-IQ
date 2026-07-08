& {
# ================================================================================================
# PPIQ M1-01 / M1-02 (Session A, Option 1-A): load the 3-month physics-linked plant into the EXISTING
# demo src_* shapes, then register -> Stage-1 -> Stage-2 -> prove material_units at plant scale.
# NO ladder change, NO eradication debt: data written into src_meltshop_pg.heats /
# src_caster_oracle_shape.{cast_sequence,cast_pieces} / src_hsm_oracle_shape.hsm_coils /
# src_inspection_mysql_shape.{parsytec_surface_defects,downtime_events} / src_pkl_mssql_shape.pickle_orders,
# which the current Stage-2 ladder already maps. Genealogy heat_no -> piece(slab) -> coil carried through.
# ================================================================================================
$ErrorActionPreference='Stop'
$R='C:\Workspace\PlantProcess-IQ'
$env:PGPASSWORD='ppiq_dev_local_only'
$psql=(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' | Sort-Object FullName -Descending | Select-Object -First 1).FullName
$gz = Join-Path $R 'loadA.sql.gz'
if (-not (Test-Path $gz)) { throw 'place loadA.sql.gz in the repo root first' }

Write-Host '[1/5] Decompress the plant SQL'
$sqlPath = Join-Path $env:TEMP 'loadA.sql'
$in=[System.IO.File]::OpenRead($gz); $gzs=New-Object System.IO.Compression.GzipStream($in,[System.IO.Compression.CompressionMode]::Decompress)
$out=[System.IO.File]::Create($sqlPath); $gzs.CopyTo($out); $out.Close();$gzs.Close();$in.Close()
Write-Host ('  ' + [math]::Round((Get-Item $sqlPath).Length/1MB,1) + ' MB')

Write-Host '[2/5] Load into src_* shapes (existing pipeline input)'
& $psql -h localhost -U ppiq_dev -d ppiq_app -v ON_ERROR_STOP=1 -q -f $sqlPath
if ($LASTEXITCODE -ne 0) { throw 'load failed' }

Write-Host '[3/5] Stage-1 + Stage-2 via the product API'
$tok=(Invoke-RestMethod -Method Post -Uri 'http://localhost:5063/auth/login' -ContentType 'application/json' -Body (@{username='e2eadmin';password='E2EAdmin123!'}|ConvertTo-Json)).accessToken
$H=@{Authorization='Bearer '+$tok}
Invoke-RestMethod -Method Post -Uri 'http://localhost:5063/admin/two-stage-import/stage1/run' -Headers $H -ContentType 'application/json' -Body '{"requestedBy":"sessionA"}' | Out-Null
Start-Sleep -Seconds 8
Invoke-RestMethod -Method Post -Uri 'http://localhost:5063/admin/two-stage-import/stage2/run' -Headers $H -ContentType 'application/json' -Body '{"requestedBy":"sessionA"}' | Out-Null
Start-Sleep -Seconds 5

Write-Host '[4/5] PROOF - material_units at plant scale + genealogy'
& $psql -h localhost -U ppiq_dev -d ppiq_app -c "SELECT material_unit_type, count(*) FROM material_units WHERE is_deleted=false GROUP BY 1 ORDER BY 1;"
& $psql -h localhost -U ppiq_dev -d ppiq_app -c "SELECT 'genealogy_edges='||count(*) FROM genealogy_edges WHERE is_deleted=false;"
& $psql -h localhost -U ppiq_dev -d ppiq_app -c "SELECT 'quality_events='||count(*) FROM quality_events WHERE is_deleted=false;"

Write-Host '[5/5] Genealogy walk on a real plant coil (heat->slab->coil)'
& $psql -h localhost -U ppiq_dev -d ppiq_app -c "SELECT mu.material_code, mu.material_unit_type FROM material_units mu WHERE mu.material_unit_type='Coil' AND mu.is_deleted=false ORDER BY mu.created_at_utc LIMIT 1;"
Write-Host ''
Write-Host 'DONE. Expect Coil ~18629 + Slab ~18629 + heat lots; genealogy_edges > 30000; quality_events > 100000.'
Write-Host 'If material_unit_type shows plant scale -> M1-01+M1-02 acceptance MET on the existing ladder, zero code change.'
Write-Host 'Then: bind a widget to a canonical view (J6) and run correlation (J7) on REAL plant data.'
}
