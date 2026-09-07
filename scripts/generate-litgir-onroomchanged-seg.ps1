$ErrorActionPreference = 'Stop'

param(
    [string]$LitgirRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'litgir'),
    [string]$SegusumRoot = (Split-Path -Parent $PSScriptRoot)
)

$segusumRoot = (Resolve-Path $SegusumRoot).Path
$litgirRoot = (Resolve-Path $LitgirRoot).Path

$demoRoot = Join-Path $env:TEMP 'segusum-litgir-demo'

$tempSource = Join-Path $demoRoot 'worldOnRoomChanged.cs'
$migrationSource = Join-Path $litgirRoot 'docs\migration-inputs\worldOnRoomChanged.cs'
$liveSource = Join-Path $litgirRoot 'WebApiLitGir\worldOnRoomChanged.cs'

$worldObjectsSource = Join-Path $litgirRoot 'WebApiLitGir\worldObjects.cs'
$tempWorldObjects = Join-Path $demoRoot 'worldObjects.cs'

$tempOutput = Join-Path $env:TEMP 'worldOnRoomChanged.generated.seg'
$runtimeOutput = Join-Path $litgirRoot 'WebApiLitGir\Gameplay\OnRoomChanged.seg'

New-Item -ItemType Directory -Force $demoRoot | Out-Null

# Il primo run congela il C# completo. Dopo quel momento il frozen source
# è l'unica sorgente autorevole e il workflow è ripetibile/idempotente.
if ((Test-Path -LiteralPath $liveSource) -and -not (Test-Path -LiteralPath $migrationSource)) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $migrationSource) | Out-Null
    Copy-Item -LiteralPath $liveSource -Destination $migrationSource -Force
}
if (Test-Path -LiteralPath $migrationSource) {
    $inputSource = $migrationSource
}
elseif (Test-Path -LiteralPath $liveSource) {
    $inputSource = $liveSource
}
else {
    throw "C# migration source not found. Expected either '$liveSource' or '$migrationSource'."
}

Copy-Item -LiteralPath $inputSource -Destination $tempSource -Force
Copy-Item -LiteralPath $worldObjectsSource -Destination $tempWorldObjects -Force

Write-Host ""
Write-Host "Source:"
Write-Host $inputSource
Write-Host ""

$cliArgs = @(
    'run',
    '--project',
    (Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj'),
    '--no-restore',
    '--',
    'migrate-csharp',
    $tempSource,
    '--world',
    'game',
    '--output',
    $tempOutput,
    '--emit-partial'
)

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

$cliOutput = & dotnet @cliArgs 2>&1
$cliExit = $LASTEXITCODE

$ErrorActionPreference = $previousErrorActionPreference

$cliOutput | ForEach-Object { Write-Host $_ }

# La CLI usa anche exit code 1 per generazione con diagnostiche.
# Consideriamo fallimento vero solo > 1.
if ($cliExit -gt 1) {
    throw "Migration CLI failed with exit code $cliExit."
}

if (-not (Test-Path -LiteralPath $tempOutput)) {
    throw "Generated SEG file not found: $tempOutput"
}

$runtimeDirectory = Split-Path -Parent $runtimeOutput
New-Item -ItemType Directory -Force $runtimeDirectory | Out-Null

# In questa fase di review vogliamo SEMPRE vedere l'ultimo SEG generato,
# anche se la CLI ha emesso diagnostiche.
Copy-Item -LiteralPath $tempOutput -Destination $runtimeOutput -Force

# Derive C#/SEG symbol ownership from the parsed SEG AST. This is intentionally
# data-driven: declarations owned by SEG are removed from active C#, while
# reference-only IDs remain there (or are recreated in a generated bridge if
# a previous migration left them absent).
$ownershipArgs = @(
    'run', '--project', (Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj'), '--no-restore', '--',
    'audit-ownership', $tempOutput,
    '--runtime-root', (Join-Path $litgirRoot 'WebApiLitGir'),
    '--history', $migrationSource,
    '--apply'
)
$ownershipOutput = & dotnet @ownershipArgs 2>&1
$ownershipExit = $LASTEXITCODE
$ownershipOutput | ForEach-Object { Write-Host $_ }
if ($ownershipExit -ne 0) { throw "SEG/C# ownership synchronization failed with exit code $ownershipExit." }

$manualMarkers = @(
    Select-String `
        -LiteralPath $tempOutput `
        -Pattern 'C2SEG-MANUAL' `
        -SimpleMatch
)

Write-Host ""
Write-Host "Generated temp:"
Write-Host $tempOutput

Write-Host ""
Write-Host "Updated runtime:"
Write-Host $runtimeOutput

Write-Host ""
Write-Host "CLI exit code:"
Write-Host $cliExit

Write-Host ""
Write-Host "C2SEG-MANUAL:"
Write-Host $manualMarkers.Count

Write-Host ""
Write-Host "Done."
