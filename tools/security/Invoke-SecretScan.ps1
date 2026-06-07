[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$PlantFakeSecretProbe
)

$ErrorActionPreference = "Stop"

Push-Location $ProjectRoot
try {
    $gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue

    if ($gitleaks) {
        Write-Host "Running gitleaks detect..." -ForegroundColor Cyan
        gitleaks detect --source . --redact --no-git
        if ($LASTEXITCODE -ne 0) { throw "gitleaks failed." }

        if ($PlantFakeSecretProbe) {
            $probe = Join-Path $ProjectRoot ".ppiq_fake_secret_probe.txt"
            "AWS_SECRET_ACCESS_KEY=FAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKE" | Set-Content -Path $probe -Encoding ASCII
            try {
                gitleaks detect --source . --redact --no-git | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    throw "PPIQ-T012 failed: planted fake secret was not detected by gitleaks."
                }
                Write-Host "Fake secret probe was detected as expected." -ForegroundColor Green
            }
            finally {
                Remove-Item $probe -Force -ErrorAction SilentlyContinue
            }
        }

        Write-Host "PPIQ-T012 passed: gitleaks scan completed." -ForegroundColor Green
        return
    }

    Write-Host "gitleaks not installed. Running fallback runtime-config scanner." -ForegroundColor Yellow

    $patterns = @(
        "password\s*=\s*[^\s;`"']{8,}",
        "secret\s*=\s*[^\s;`"']{8,}",
        "clientsecret",
        "aws_secret_access_key",
        "private_key",
        "BEGIN RSA PRIVATE KEY",
        "BEGIN PRIVATE KEY"
    )

    $scanRoots = @(
        "Backend",
        "deploy",
        "Infrastructure",
        "."
    )

    $skip = @(
        "\\bin\\",
        "\\obj\\",
        "\\node_modules\\",
        "\\.git\\",
        "\\dist\\",
        "\\coverage\\",
        "\\docs\\pack-g\\",
        "\\.realization_backup\\"
    )

    $extensions = @(".json",".yml",".yaml",".env",".example",".production",".template",".ps1",".sh",".sql",".config")

    $findings = New-Object System.Collections.Generic.List[string]

    foreach ($rootName in $scanRoots) {
        $dir = Join-Path $ProjectRoot $rootName
        if (-not (Test-Path $dir)) { continue }

        Get-ChildItem -Path $dir -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $path = $_.FullName
            $relative = $path.Substring($ProjectRoot.Length).TrimStart("\")
            foreach ($s in $skip) {
                if ($path -match $s) { return }
            }

            if ($extensions -notcontains $_.Extension.ToLowerInvariant() -and $_.Name -notmatch "^\.env") { return }

            $text = Get-Content -Path $path -Raw -ErrorAction SilentlyContinue
            foreach ($pattern in $patterns) {
                if ($text -match $pattern) {
                    $findings.Add("$relative -> $pattern")
                    break
                }
            }
        }
    }

    if ($findings.Count -gt 0) {
        Write-Host "Fallback secret scan findings:" -ForegroundColor Red
        $findings | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
        throw "PPIQ-T012 failed: fallback scanner found possible secrets."
    }

    Write-Host "PPIQ-T012 passed: fallback runtime-config scanner found no secrets." -ForegroundColor Green
}
finally {
    Pop-Location
}
