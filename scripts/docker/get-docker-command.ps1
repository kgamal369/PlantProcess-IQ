$CandidatePaths = @(
    "docker",
    "C:\Program Files\Docker\Docker\resources\bin\docker.exe",
    "C:\Program Files\Docker\Docker\resources\docker.exe"
)

foreach ($Candidate in $CandidatePaths) {
    $Command = Get-Command $Candidate -ErrorAction SilentlyContinue
    if ($Command) {
        Write-Output $Command.Source
        exit 0
    }

    if (Test-Path $Candidate) {
        Write-Output $Candidate
        exit 0
    }
}

exit 1
