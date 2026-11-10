# Starts the API against the rentdb-pg Docker database on port 5433.
# The password is read from the container, so it is never stored in a file.
$ErrorActionPreference = 'Stop'
$pw = (docker inspect rentdb-pg --format '{{range .Config.Env}}{{println .}}{{end}}' |
    Where-Object { $_ -like 'POSTGRES_PASSWORD=*' }) -replace '^POSTGRES_PASSWORD=', ''
$env:ConnectionStrings__RentDb = "Host=localhost;Port=5433;Database=rentdb_v2;Username=postgres;Password=$pw"
Set-Location (Join-Path $PSScriptRoot '..\src\RentTracker.Api')
dotnet run --no-launch-profile
