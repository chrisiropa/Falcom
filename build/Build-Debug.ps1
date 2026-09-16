param(
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $projectRoot 'Falcom.slnx'

try {
    Write-Host 'Baue FALCOM in Debug...' -ForegroundColor Cyan
    & dotnet build $solution -c Debug
    if ($LASTEXITCODE -ne 0) {
        throw "Debug-Build fehlgeschlagen (dotnet exit code $LASTEXITCODE)."
    }

    Write-Host 'FALCOM Debug-Build ist aktuell.' -ForegroundColor Green
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
finally {
    if (-not $NoPause) {
        Read-Host 'Enter zum Schliessen'
    }
}
