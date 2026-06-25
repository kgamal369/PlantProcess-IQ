param([Parameter(Mandatory)][string]$EnvFile,[Parameter(Mandatory)][string]$PreserveDir,[string]$Template='env/profiles/server.env.example')
$ErrorActionPreference='Stop'; $enc=New-Object System.Text.UTF8Encoding($false)
$preserveEnv=Join-Path $PreserveDir '.env'; $credFile=Join-Path $PreserveDir 'FIRST_LOGIN.txt'
New-Item -ItemType Directory -Force -Path $PreserveDir,(Split-Path $EnvFile)|Out-Null
function Gen([int]$b){$x=New-Object byte[] $b;[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($x);($x|%{$_.ToString('x2')}) -join ''}
if(Test-Path $preserveEnv){Copy-Item $preserveEnv $EnvFile -Force;Write-Host 'ensure-runtime-env: reused persisted secrets';return}
if(Test-Path $EnvFile){Copy-Item $EnvFile $preserveEnv -Force;Write-Host 'ensure-runtime-env: adopted operator-provided .env';return}
Write-Host 'ensure-runtime-env: generating fresh runtime secrets'
$pg=Gen 24;$sign=Gen 48;$au='ppiq-owner';$ap=Gen 16
if(Test-Path $Template){Copy-Item $Template $EnvFile -Force}else{[System.IO.File]::WriteAllText($EnvFile,'',$enc)}
$lines=[System.Collections.Generic.List[string]]::new();[System.IO.File]::ReadAllLines($EnvFile)|%{[void]$lines.Add($_)}
for($i=$lines.Count-1;$i -ge 0;$i--){if($lines[$i] -match '_Password_REMOVED_FROM_TRACKED_TEMPLATE='){$lines.RemoveAt($i)}}
function S($k,$v){for($i=0;$i -lt $lines.Count;$i++){if($lines[$i] -match ('^'+[regex]::Escape($k)+'=')){$lines[$i]="$k=$v";return}};[void]$lines.Add("$k=$v")}
function V($k){$h=$lines|?{$_ -match ('^'+[regex]::Escape($k)+'=')}|Select -First 1;if($h){($h -split '=',2)[1]}else{''}}
$u=V 'POSTGRES_USER';if(-not $u){$u='plantprocess'};$d=V 'POSTGRES_DB';if(-not $d){$d='plantprocessiq'};$pt=V 'POSTGRES_PORT';if(-not $pt){$pt='5432'}
$h='plantprocess-postgres'
S 'POSTGRES_HOST' $h
S 'POSTGRES_PASSWORD' $pg
S 'ConnectionStrings__PlantProcessDb' ("Host=$h;Port=$pt;Database=$d;Username=$u;Password=$pg")
S 'PlantProcess__Auth__SigningKey' $sign
S 'PlantProcess__Auth__BootstrapAdminPassword' '__DISABLED__'; S 'PPIQ_BOOTSTRAP_ADMIN_PASSWORD' '__DISABLED__'
S 'PlantProcess__Auth__Users__0__UserName' $au; S 'PlantProcess__Auth__Users__0__Password' $ap
S 'PlantProcess__Auth__Users__0__Role' 'Admin'; S 'PlantProcess__Auth__Users__0__IsBootstrapAdmin' 'false'
S 'PPIQ_SMOKE_USERNAME' $au; S 'PPIQ_SMOKE_PASSWORD' $ap; S 'VITE_SMOKE_USERNAME' $au; S 'VITE_SMOKE_PASSWORD' $ap
S 'PPIQ_DEMO_SOURCES_MODE' 'disabled'
S 'PPIQ_RUN_E2E' 'off'
S 'SITE_HOST' 'localhost'; S 'WEBSITE_HOST' 'website.localhost'
S 'CADDY_AUTO_HTTPS' 'off'; S 'ACME_EMAIL' 'admin@example.invalid'
S 'PPIQ_API_UPSTREAM' 'plantprocess-api:5063'; S 'PPIQ_APP_UPSTREAM' 'plantprocess-web:80'; S 'PPIQ_WEBSITE_UPSTREAM' 'plantprocess-web:80'
[System.IO.File]::WriteAllText($EnvFile,(($lines -join "`n")+"`n"),$enc)
Copy-Item $EnvFile $preserveEnv -Force
[System.IO.File]::WriteAllText($credFile,(@("PlantProcess IQ first-login owner",("username: "+$au),("password: "+$ap),"Rotate after first login.") -join "`n"),$enc)
Write-Host ("ensure-runtime-env: generated + persisted; first-login creds in "+$credFile)
