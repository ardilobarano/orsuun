# Brings the local dev stack up if it is not already: PostgreSQL (user process) and the game server.
# Safe to run any time; it does nothing for parts that are already healthy.
#   tools\dev-up.ps1            start what is missing
#   tools\dev-up.ps1 -Status    report only
#   tools\dev-up.ps1 -Rebuild   rebuild the server before starting it (after code changes)
param([switch]$Status, [switch]$Rebuild)

$root = Split-Path $PSScriptRoot -Parent
$pgBin = "C:\Program Files\PostgreSQL\16\bin"
$pgData = Join-Path $env:LOCALAPPDATA "Orsuun\pgdata"
$pgLog = Join-Path $env:LOCALAPPDATA "Orsuun\postgres.log"
$dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
$logDir = Join-Path $root "artifacts"
New-Item -ItemType Directory -Force $logDir | Out-Null

function Test-Pg {
    & "$pgBin\pg_isready.exe" -h localhost -q 2>$null | Out-Null
    return ($LASTEXITCODE -eq 0)
}
function Test-Server {
    try { return [bool](Invoke-RestMethod "http://localhost:5080/health" -TimeoutSec 2).ok } catch { return $false }
}

$pgOk = Test-Pg
$serverOk = Test-Server
"postgres: " + $(if ($pgOk) { "up" } else { "down" })
"server:   " + $(if ($serverOk) { "up" } else { "down" })
if ($Status) { exit 0 }

if (-not $pgOk) {
    # The service form is registered only if the user ran tools\pg-service.ps1 as admin; otherwise a user process.
    $svc = Get-Service postgresql-orsuun -ErrorAction SilentlyContinue
    if ($svc) { try { Start-Service postgresql-orsuun -ErrorAction Stop } catch { "service refused to start ($($_.Exception.Message.Trim())); falling back to a user process" } }
    Start-Sleep 2
    if (-not (Test-Pg)) { Start-Process -FilePath "$pgBin\pg_ctl.exe" -ArgumentList "-D `"$pgData`" -l `"$pgLog`" -w start" -WindowStyle Hidden -Wait }
    for ($i = 0; $i -lt 20 -and -not (Test-Pg); $i++) { Start-Sleep 1 }
    "postgres: " + $(if (Test-Pg) { "started" } else { "FAILED to start, see $pgLog" })
}

if ($Rebuild -and $serverOk) {
    Get-Process Orsuun.Server -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 1
    $serverOk = $false
}
if ($Rebuild) {
    $env:DOTNET_NOLOGO = '1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    & $dotnet build (Join-Path $root "src\Orsuun.Server") -c Debug --nologo -v q | Select-String -Pattern ' error |Build succeeded'
}

if (-not $serverOk) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_NOLOGO = '1'; $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $p = Start-Process -FilePath $dotnet -ArgumentList 'run', '--project', (Join-Path $root "src\Orsuun.Server"), '--no-build', '-c', 'Debug' `
        -RedirectStandardOutput (Join-Path $logDir "server.log") -RedirectStandardError (Join-Path $logDir "server.log.err") -PassThru -WindowStyle Hidden
    $p.Id | Out-File (Join-Path $logDir "server.pid")
    for ($i = 0; $i -lt 40 -and -not (Test-Server); $i++) { Start-Sleep 1 }
    "server:   " + $(if (Test-Server) { "started (pid $($p.Id))" } else { "FAILED to start, see artifacts\server.log.err" })
}
