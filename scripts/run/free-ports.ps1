param(
    [int[]]$Ports = @(5063, 5173, 5080),
    [switch]$Force
)

$ErrorActionPreference = "Stop"

foreach ($Port in $Ports) {
    $Connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue

    if (-not $Connections) {
        Write-Host "[FREE] Port $Port" -ForegroundColor Green
        continue
    }

    foreach ($Connection in $Connections) {
        $PidValue = $Connection.OwningProcess
        $Process = Get-Process -Id $PidValue -ErrorAction SilentlyContinue

        if (-not $Process) {
            continue
        }

        Write-Host "[USED] Port $Port by PID=$PidValue Name=$($Process.ProcessName) Path=$($Process.Path)" -ForegroundColor Yellow

        if ($Force) {
            Stop-Process -Id $PidValue -Force
            Write-Host "[KILLED] PID=$PidValue on port $Port" -ForegroundColor Green
        }
        else {
            Write-Host "       Use -Force to kill this process." -ForegroundColor DarkGray
        }
    }
}
