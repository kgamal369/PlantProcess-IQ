param(
  [Parameter(Mandatory)][string]$EnvFile,
  [Parameter(Mandatory)][string]$PreserveDir,
  [string]$Template = 'env/profiles/server.env.example'
)
$ErrorActionPreference = 'Stop'
$enc = New-Object System.Text.UTF8Encoding($false)
$preserveEnv = Join-Path $PreserveDir '.env'
$credFile    = Join-Path $PreserveDir 'FIRST_LOGIN.txt'
New-Item -ItemType Directory -Force -Path $PreserveDir,(Split-Path $EnvFile) | Out-Null
function Gen([int]$bytes){ $b=New-Object byte[] $bytes; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); ($b|ForEach-Object{$_.ToString('x2')}) -join '' }
if (Test-Path $preserveEnv) { Copy-Item $preserveEnv $EnvFile -Force; Write-Host 'ensure-runtime-env: reused persisted secrets'; return }
if (Test-Path $EnvFile)     { Copy-Item $EnvFile $preserveEnv -Force; Write-Host 'ensure-runtime-env: adopted operator-provided .env'; return }
Write-Host 'ensure-runtime-env: generating fresh runtime secrets'
$pgPass=Gen 24; $signing=Gen 48; $adminUser='ppiq-owner'; $adminPass=Gen 16
if (Test-Path $Template) { Copy-Item $Template $EnvFile -Force } else { [System.IO.File]::WriteAllText($EnvFile,'',$enc) }
$lines=[System.Collections.Generic.List[string]]::new()
[System.IO.File]::ReadAllLines($EnvFile) | ForEach-Object { [void]$lines.Add($_) }
# strip dead template placeholder lines (mangled key names no container reads)
for ($i=$lines.Count-1; $i -ge 0; $i--) { if ($lines[$i] -match '_Password_REMOVED_FROM_TRACKED_TEMPLATE=') { $lines.RemoveAt($i) } }
function SetKv([string]$k,[string]$v){ for($i=0;$i -lt $lines.Count;$i++){ if($lines[$i] -match ('^'+[regex]::Escape($k)+'=')){ $lines[$i]="$k=$v"; return } } [void]$lines.Add("$k=$v") }
function Val([string]$k){ $h=$lines|Where-Object{$_ -match ('^'+[regex]::Escape($k)+'=')}|Select-Object -First 1; if($h){($h -split '=',2)[1]}else{''} }
$pgUser=Val 'POSTGRES_USER'; if(-not $pgUser){$pgUser='plantprocess'}
$pgDb=Val 'POSTGRES_DB';     if(-not $pgDb){$pgDb='plantprocessiq'}
$pgHost=Val 'POSTGRES_HOST'; if(-not $pgHost){$pgHost='plantprocess-postgres'}
$pgPort=Val 'POSTGRES_PORT'; if(-not $pgPort){$pgPort='5432'}
SetKv 'POSTGRES_PASSWORD' $pgPass
SetKv 'ConnectionStrings__PlantProcessDb' ("Host=$pgHost;Port=$pgPort;Database=$pgDb;Username=$pgUser;Password=$pgPass")
SetKv 'PlantProcess__Auth__SigningKey' $signing
SetKv 'PlantProcess__Auth__BootstrapAdminPassword' '__DISABLED__'
SetKv 'PPIQ_BOOTSTRAP_ADMIN_PASSWORD' '__DISABLED__'
SetKv 'PlantProcess__Auth__Users__0__UserName' $adminUser
SetKv 'PlantProcess__Auth__Users__0__Password' $adminPass
SetKv 'PlantProcess__Auth__Users__0__Role' 'Admin'
SetKv 'PlantProcess__Auth__Users__0__IsBootstrapAdmin' 'false'
SetKv 'PPIQ_SMOKE_USERNAME' $adminUser; SetKv 'PPIQ_SMOKE_PASSWORD' $adminPass
SetKv 'VITE_SMOKE_USERNAME' $adminUser; SetKv 'VITE_SMOKE_PASSWORD' $adminPass
[System.IO.File]::WriteAllText($EnvFile, (($lines -join "`n") + "`n"), $enc)
Copy-Item $EnvFile $preserveEnv -Force
$cred=@("PlantProcess IQ first-login owner ($([DateTime]::UtcNow.ToString('s'))Z)","username: $adminUser","password: $adminPass","Rotate after first login. Secrets at $preserveEnv.") -join "`n"
[System.IO.File]::WriteAllText($credFile, $cred, $enc)
Write-Host "ensure-runtime-env: generated + persisted; first-login creds in $credFile"