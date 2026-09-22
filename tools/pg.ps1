# Local PostgreSQL 16 for development. It runs as a user process (no admin needed), data lives in
# %LOCALAPPDATA%\Orsuun\pgdata.   tools\pg.ps1 start | stop | status | psql
param([ValidateSet('start', 'stop', 'status', 'psql')][string]$Action = 'status')

$bin = "C:\Program Files\PostgreSQL\16\bin"
$data = Join-Path $env:LOCALAPPDATA "Orsuun\pgdata"
$log = Join-Path $env:LOCALAPPDATA "Orsuun\postgres.log"

switch ($Action) {
    'start'  { Start-Process -FilePath "$bin\pg_ctl.exe" -ArgumentList "-D `"$data`" -l `"$log`" start" -WindowStyle Hidden -Wait; & "$bin\pg_isready.exe" -h localhost }
    'stop'   { & "$bin\pg_ctl.exe" -D $data stop }
    'status' { & "$bin\pg_isready.exe" -h localhost }
    'psql'   { $env:PGPASSWORD = 'orsuun-dev'; & "$bin\psql.exe" -U orsuun -h localhost -d orsuun }
}
