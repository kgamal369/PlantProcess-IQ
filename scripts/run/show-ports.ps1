param(
    [int[]]$Ports = @(5063, 5173, 5080, 5432)
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

        if ($Process) {
            Write-Host "[USED] Port $Port by PID=$PidValue Name=$($Process.ProcessName) Path=$($Process.Path)" -ForegroundColor Yellow
        }
        else {
            Write-Host "[USED] Port $Port by PID=$PidValue" -ForegroundColor Yellow
        }
    }
}
