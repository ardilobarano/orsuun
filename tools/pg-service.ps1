# Registers the dev PostgreSQL as a Windows service that starts with the machine. Needs an ADMIN PowerShell:
#   Start-Process powershell -Verb RunAs -ArgumentList '-ExecutionPolicy Bypass -File C:\Users\uardi\Orsuun\tools\pg-service.ps1'
# Without this, tools\dev-up.ps1 runs PostgreSQL as a user process at logon instead.

$pgBin = "C:\Program Files\PostgreSQL\16\bin"
$pgData = Join-Path $env:LOCALAPPDATA "Orsuun\pgdata"
$pgLog = Join-Path $env:LOCALAPPDATA "Orsuun\postgres.log"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Run this in an elevated (Administrator) PowerShell." }

# Stop the user-process instance if one is running; the service takes over the same data directory.
& "$pgBin\pg_ctl.exe" -D $pgData stop -m fast 2>$null

# -U: run the service as the current user, who owns the data directory (initdb refuses to run as admin, so
# the cluster was created as this user and must stay theirs).
$user = "$env:USERDOMAIN\$env:USERNAME"
& "$pgBin\pg_ctl.exe" register -N "postgresql-orsuun" -D $pgData -S auto -w -U $user
if ($LASTEXITCODE -ne 0) { throw "pg_ctl register failed (exit $LASTEXITCODE). If it asked for a password, rerun and supply your Windows password, or use dev-up.ps1 instead." }

Start-Service postgresql-orsuun
Get-Service postgresql-orsuun | Format-Table Name, Status, StartType -AutoSize
& "$pgBin\pg_isready.exe" -h localhost
