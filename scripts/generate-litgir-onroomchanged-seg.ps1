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

# Prefer the live C# source while it still exists.
# After the real migration, use the frozen migration input.
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

if ($cliExit -ne 0) {
    throw "Migration CLI failed with exit code $cliExit. Runtime SEG was NOT updated."
}

if (-not (Test-Path -LiteralPath $tempOutput)) {
    throw "Generated SEG file not found: $tempOutput"
}

$manualMarkers = Select-String `
    -LiteralPath $tempOutput `
    -Pattern 'C2SEG-MANUAL' `
    -SimpleMatch

if ($manualMarkers.Count -gt 0) {
    throw "Generated SEG still contains $($manualMarkers.Count) C2SEG-MANUAL marker(s). Runtime SEG was NOT updated."
}

$runtimeDirectory = Split-Path -Parent $runtimeOutput
New-Item -ItemType Directory -Force $runtimeDirectory | Out-Null

Copy-Item -LiteralPath $tempOutput -Destination $runtimeOutput -Force

Write-Host ""
Write-Host "Generated temp:"
Write-Host $tempOutput
Write-Host ""
Write-Host "Updated runtime:"
Write-Host $runtimeOutput
Write-Host ""
Write-Host "C2SEG-MANUAL: 0"