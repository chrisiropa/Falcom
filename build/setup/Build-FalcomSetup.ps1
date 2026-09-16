param(
    [switch]$OpenOutput,
    [switch]$StopRunningApps
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$falcomProject = Join-Path $projectRoot 'Falcom.csproj'
$falcomWpfProject = Join-Path $projectRoot 'FalcomWpf\FalcomWpf.csproj'
$publishRoot = Join-Path $projectRoot 'publish'
$falcomPublish = Join-Path $publishRoot 'Falcom'
$falcomWpfPublish = Join-Path $publishRoot 'FalcomWpf'
$issFile = Join-Path $PSScriptRoot 'FalcomSetup.iss'
$compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$outputFile = Join-Path (Split-Path $projectRoot -Parent) 'Setups\FalcomSetup\FalcomSetup.exe'

function Get-FalcomProcesses {
    Get-Process -Name 'Falcom', 'FalcomWpf' -ErrorAction SilentlyContinue
}

function Stop-FalcomAppsForPackage {
    $processes = @(Get-FalcomProcesses)
    if ($processes.Count -eq 0) {
        return
    }

    if (-not $StopRunningApps) {
        throw 'FALCOM oder FALCOM WPF laeuft noch. Beende beide Anwendungen oder starte diesen Builder mit -StopRunningApps.'
    }

    Write-Host 'Beende lokale FALCOM-Anwendungen fuer den Debug-Publish...' -ForegroundColor Yellow
    $processes | Stop-Process -Force
    Start-Sleep -Seconds 2
}

function Reset-PublishDirectory([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

foreach ($requiredPath in @($falcomProject, $falcomWpfProject, $issFile, $compiler)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Erforderlicher Pfad fehlt: $requiredPath"
    }
}

Stop-FalcomAppsForPackage
Reset-PublishDirectory $falcomPublish
Reset-PublishDirectory $falcomWpfPublish

Write-Host 'Veroeffentliche FALCOM in Debug...' -ForegroundColor Cyan
& dotnet publish $falcomProject -c Debug -r win-x64 --self-contained false -o $falcomPublish
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host 'Veroeffentliche FALCOM WPF in Debug...' -ForegroundColor Cyan
& dotnet publish $falcomWpfProject -c Debug -r win-x64 --self-contained false -o $falcomWpfPublish
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Path (Split-Path $outputFile -Parent) -Force | Out-Null
Write-Host "Erstelle gemeinsames Setup aus: $falcomPublish und $falcomWpfPublish" -ForegroundColor Cyan
& $compiler $issFile
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Setup erstellt: $outputFile" -ForegroundColor Green
if ($OpenOutput) {
    Start-Process explorer.exe "/select,`"$outputFile`""
}
