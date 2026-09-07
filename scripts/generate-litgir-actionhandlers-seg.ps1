param(
    [string]$LitgirRoot = 'C:\Users\Maurizio\Software\litgir',
    [string]$SegusumRoot = 'C:\Users\Maurizio\Software\Segusum',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$litgirRoot = (Resolve-Path $LitgirRoot).Path
$segusumRoot = (Resolve-Path $SegusumRoot).Path
$frozen = Join-Path $litgirRoot 'docs\migration-inputs\worldActionHandlers.cs'
$runtimeRoot = Join-Path $litgirRoot 'WebApiLitGir'
$runtimeSeg = Join-Path $runtimeRoot 'Gameplay\ActionHandlers.seg'
$tempRoot = Join-Path $env:TEMP 'segusum-litgir-actionhandlers'
$tempSeg = Join-Path $tempRoot 'ActionHandlers.generated.seg'
$cliProject = Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj'

if (-not (Test-Path -LiteralPath $frozen)) {
    throw "Frozen ActionHandlers source not found: $frozen"
}
New-Item -ItemType Directory -Force $tempRoot | Out-Null
New-Item -ItemType Directory -Force (Split-Path -Parent $runtimeSeg) | Out-Null

function Invoke-Cli([string[]]$Arguments) {
    $output = & dotnet run --project $cliProject --no-restore -- @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -gt 1) { throw "Segusum CLI failed with exit code $exitCode." }
    return $output
}

Write-Host "Generating ActionHandlers SEG from frozen source: $frozen"
Invoke-Cli @(
    'migrate-csharp', $frozen,
    '--world', 'game',
    '--context-root', $runtimeRoot,
    '--emit-partial',
    '--output', $tempSeg
) | Out-Null

if (-not (Test-Path -LiteralPath $tempSeg)) {
    throw "Generated temporary SEG file not found: $tempSeg"
}

$manual = @(Select-String -LiteralPath $tempSeg -Pattern 'C2SEG-MANUAL' -SimpleMatch)
if ($manual.Count -ne 0) { throw "Generated ActionHandlers contains $($manual.Count) C2SEG-MANUAL markers." }

Write-Host "Checking generated SEG parser diagnostics"
Invoke-Cli @('parse-seg', $tempSeg) | Out-Null

Write-Host "Ownership dry run"
$dryRunOutput = Invoke-Cli @(
    'audit-ownership', $tempSeg,
    '--runtime-root', $runtimeRoot,
    '--history', $frozen
)

if ($DryRun) {
    Write-Host "Dry run requested; runtime SEG and C# ownership were not changed."
    exit 0
}

Copy-Item -LiteralPath $tempSeg -Destination $runtimeSeg -Force
Write-Host "Updated runtime SEG: $runtimeSeg"

Write-Host "Applying deterministic ownership"
Invoke-Cli @(
    'audit-ownership', $tempSeg,
    '--runtime-root', $runtimeRoot,
    '--history', $frozen,
    '--apply'
) | Out-Null

Write-Host "Generated: $runtimeSeg"
Write-Host "C2SEG-MANUAL: 0"
Write-Host "Ownership applied from SEG AST and Roslyn signatures."
