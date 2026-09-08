param(
    [string]$LitgirRoot = 'C:\Users\Maurizio\Software\litgir',
    [string]$SegusumRoot = 'C:\Users\Maurizio\Software\Segusum',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$litgirRoot = (Resolve-Path $LitgirRoot).Path
$segusumRoot = (Resolve-Path $SegusumRoot).Path
$runtimeRoot = Join-Path $litgirRoot 'WebApiLitGir'
$migrationRoot = Join-Path $litgirRoot 'docs\migration-inputs'
$cli = Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj'
$tempRoot = Join-Path $env:TEMP 'segusum-litgir-gameplay-helpers'
New-Item -ItemType Directory -Force $migrationRoot, $tempRoot | Out-Null
$contextStage = Join-Path $tempRoot 'context'
if (Test-Path -LiteralPath $contextStage) { Remove-Item -LiteralPath $contextStage -Recurse -Force }
New-Item -ItemType Directory -Force $contextStage | Out-Null

function Ensure-Frozen([string]$live, [string]$frozen) {
    if (-not (Test-Path -LiteralPath $frozen)) {
        if (-not (Test-Path -LiteralPath $live)) { throw "Source not found: $live" }
        Copy-Item -LiteralPath $live -Destination $frozen
        Write-Host "Created frozen source: $frozen"
    }
    if (-not (Test-Path -LiteralPath $frozen)) { throw "Frozen source not found: $frozen" }
}

function Invoke-Cli([string[]]$Arguments) {
    $saved = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $lines = @(& dotnet run --project $cli --no-restore -- @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $saved
    $lines | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) { throw "Segusum CLI failed with exit code $exitCode." }
    return $lines
}

$actionFrozen = Join-Path $migrationRoot 'worldActionHandlersGameplayHelpers.cs'
$roomFrozen = Join-Path $migrationRoot 'worldOnRoomChangedHelpers.cs'
$dependencyFrozen = Join-Path $migrationRoot 'worldAfterActionExecuted.cs'
$actionLive = Join-Path $runtimeRoot 'worldActionHandlersGameplayHelpers.cs'
$roomLive = Join-Path $runtimeRoot 'worldOnRoomChangedHelpers.cs'
Ensure-Frozen $actionLive $actionFrozen
Ensure-Frozen $roomLive $roomFrozen
if (-not (Test-Path -LiteralPath $dependencyFrozen)) { throw "Reachable helper dependency frozen source not found: $dependencyFrozen" }
Get-ChildItem -LiteralPath $runtimeRoot -Filter '*.cs' -File | Copy-Item -Destination $contextStage
Copy-Item -LiteralPath $dependencyFrozen -Destination $contextStage

$actionArtifact = Join-Path $tempRoot 'ActionHandlersGameplayHelpers.generated.seg'
$roomArtifact = Join-Path $tempRoot 'OnRoomChangedHelpers.generated.seg'
$actionMerged = Join-Path $tempRoot 'ActionHandlers.merged.seg'
$roomMerged = Join-Path $tempRoot 'OnRoomChanged.merged.seg'
$actionRuntime = Join-Path $runtimeRoot 'Gameplay\ActionHandlers.seg'
$roomRuntime = Join-Path $runtimeRoot 'Gameplay\OnRoomChanged.seg'

Invoke-Cli @('migrate-csharp', $actionFrozen, '--world', 'game', '--context-root', $contextStage, '--emit-partial', '--output', $actionArtifact) | Out-Null
Invoke-Cli @('migrate-csharp', $roomFrozen, '--world', 'game', '--context-root', $contextStage, '--emit-partial', '--output', $roomArtifact) | Out-Null
Invoke-Cli @('merge-seg', $actionRuntime, $actionArtifact, '--output', $actionMerged) | Out-Null
Invoke-Cli @('merge-seg', $roomRuntime, $roomArtifact, '--output', $roomMerged) | Out-Null

Write-Host 'Ownership dry run: ActionHandlers'
Invoke-Cli @('audit-ownership', $actionMerged, '--runtime-root', $runtimeRoot, '--history', $actionFrozen) | Out-Null
Write-Host 'ActionHandlers ownership dry run complete'
Write-Host 'Ownership dry run: OnRoomChanged'
Invoke-Cli @('audit-ownership', $roomMerged, '--runtime-root', $runtimeRoot, '--history', $roomFrozen) | Out-Null
Write-Host 'OnRoomChanged ownership dry run complete'

if ($DryRun) { Write-Host 'Dry run complete; runtime SEG/C# unchanged.'; exit 0 }

Copy-Item -LiteralPath $actionMerged -Destination $actionRuntime -Force
Copy-Item -LiteralPath $roomMerged -Destination $roomRuntime -Force
Invoke-Cli @('audit-ownership', $actionRuntime, '--runtime-root', $runtimeRoot, '--history', $actionFrozen, '--apply') | Out-Null
Invoke-Cli @('audit-ownership', $roomRuntime, '--runtime-root', $runtimeRoot, '--history', $roomFrozen, '--apply') | Out-Null

foreach ($helper in @($actionLive, $roomLive)) {
    if (-not (Test-Path -LiteralPath $helper)) { continue }
    $remaining = Get-Content -LiteralPath $helper -Raw
    if ($remaining -notmatch '(?m)^\s*(private|protected|internal|public)\s+[\w<>,\[\]?]+\s+\w+\s*\(') {
        Remove-Item -LiteralPath $helper -Force
        Write-Host "Removed empty helper source: $helper"
    }
}

Write-Host "Generated: $actionRuntime"
Write-Host "Generated: $roomRuntime"
