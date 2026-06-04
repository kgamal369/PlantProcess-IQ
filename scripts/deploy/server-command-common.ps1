$ErrorActionPreference = "Stop"
$PpiqRepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Resolve-PpiqDockerCommand {
    $Found = Get-Command docker -ErrorAction SilentlyContinue

    if ($Found) {
        return $Found.Source
    }

    $Helper = Join-Path $PpiqRepoRoot "scripts\docker\get-docker-command.ps1"

    if (Test-Path $Helper) {
        try {
            $Output = & $Helper 2>$null

            foreach ($Line in $Output) {
                $Text = [string]$Line

                if ($Text -match "([A-Z]:\\.*docker\.exe)") {
                    $Candidate = $Matches[1]

                    if (Test-Path $Candidate) {
                        return $Candidate
                    }
                }
            }
        }
        catch {
        }
    }

    $KnownDocker = "C:\Program Files\Docker\Docker\resources\bin\docker.exe"

    if (Test-Path $KnownDocker) {
        return $KnownDocker
    }

    return $null
}

function Resolve-PpiqPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return (Join-Path $PpiqRepoRoot $Path)
}

function Get-PpiqComposeFiles {
    param([switch]$IncludeDemoSources)

    $Files = New-Object System.Collections.Generic.List[string]

    $Candidates = @(
        "deploy\server\docker-compose.server.yml",
        "deploy\docker-compose.yml",
        "docker-compose.yml"
    )

    foreach ($Candidate in $Candidates) {
        $FullPath = Join-Path $PpiqRepoRoot $Candidate

        if (Test-Path $FullPath) {
            if (-not $Files.Contains($FullPath)) {
                $Files.Add($FullPath) | Out-Null
            }
        }
    }

    if ($IncludeDemoSources) {
        $DemoCompose = Join-Path $PpiqRepoRoot "deploy\demo-sources\docker-compose.demo-sources.yml"

        if (Test-Path $DemoCompose) {
            if (-not $Files.Contains($DemoCompose)) {
                $Files.Add($DemoCompose) | Out-Null
            }
        }
    }

    return @($Files)
}

function Assert-PpiqRuntimeEnvFile {
    param([string]$EnvFile)

    $FullEnvFile = Resolve-PpiqPath $EnvFile

    if (-not (Test-Path $FullEnvFile)) {
        throw "Runtime env file not found: $FullEnvFile. Create it from deploy/server/.env.example and keep it untracked."
    }

    if ($FullEnvFile -match "\.env\.example$") {
        throw "Do not use .env.example for runtime start/stop. Use deploy/server/.env.production or another ignored runtime env file."
    }

    return $FullEnvFile
}

function Build-PpiqComposeArgs {
    param(
        [string]$EnvFile,
        [string[]]$ComposeFiles,
        [string[]]$CommandArgs
    )

    $Args = New-Object System.Collections.Generic.List[string]
    $Args.Add("compose") | Out-Null
    $Args.Add("--env-file") | Out-Null
    $Args.Add($EnvFile) | Out-Null

    foreach ($ComposeFile in $ComposeFiles) {
        $Args.Add("-f") | Out-Null
        $Args.Add($ComposeFile) | Out-Null
    }

    foreach ($Arg in $CommandArgs) {
        $Args.Add($Arg) | Out-Null
    }

    return @($Args)
}

