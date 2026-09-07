$ErrorActionPreference = 'Stop'

$segusumRoot = Split-Path -Parent $PSScriptRoot
$litgirRoot = Join-Path (Split-Path -Parent $segusumRoot) 'litgir'

$demoRoot = Join-Path $env:TEMP 'segusum-litgir-demo'

$tempSource = Join-Path $demoRoot 'worldOnRoomChanged.cs'
$liveSource = Join-Path $litgirRoot 'WebApiLitGir\worldOnRoomChanged.cs'
$migrationSource = Join-Path $litgirRoot 'docs\migration-inputs\worldOnRoomChanged.cs'

$worldObjectsSource = Join-Path $litgirRoot 'WebApiLitGir\worldObjects.cs'
$tempWorldObjects = Join-Path $demoRoot 'worldObjects.cs'

$tempOutput = Join-Path $env:TEMP 'worldOnRoomChanged.generated.seg'
$runtimeOutput = Join-Path $litgirRoot 'WebApiLitGir\Gameplay\OnRoomChanged.seg'

New-Item -ItemType Directory -Force $demoRoot | Out-Null

# Preferisci il C# runtime se esiste ancora.
# Dopo la migrazione usa la copia congelata sotto docs/migration-inputs.
if (Test-Path -LiteralPath $liveSource) {
    $inputSource = $liveSource
}
elseif (Test-Path -LiteralPath $migrationSource) {
    $inputSource = $migrationSource
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
