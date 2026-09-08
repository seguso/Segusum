param(
    [string]$LitgirRoot = 'C:\Users\Maurizio\Software\litgir',
    [string]$SegusumRoot = 'C:\Users\Maurizio\Software\Segusum',
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$litgirRoot = (Resolve-Path $LitgirRoot).Path
$segusumRoot = (Resolve-Path $SegusumRoot).Path
$frozen = Join-Path $litgirRoot 'docs\migration-inputs\worldAfterActionExecuted.cs'
$runtimeRoot = Join-Path $litgirRoot 'WebApiLitGir'
$runtimeSeg = Join-Path $runtimeRoot 'Gameplay\AfterActionExecuted.seg'
$tempRoot = Join-Path $env:TEMP 'segusum-litgir-after-action-executed'
$tempSeg = Join-Path $tempRoot 'AfterActionExecuted.generated.seg'
$ownershipRoot = Join-Path $tempRoot 'ownership-runtime'
$runtimeSource = Join-Path $runtimeRoot 'worldAfterActionExecuted.cs'
$cliProject = Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj'

if (-not (Test-Path -LiteralPath $frozen)) { throw "Frozen source not found: $frozen" }
New-Item -ItemType Directory -Force $tempRoot | Out-Null
New-Item -ItemType Directory -Force (Split-Path -Parent $runtimeSeg) | Out-Null

function Invoke-Cli([string[]]$Arguments) {
    $output = & dotnet run --project $cliProject --no-restore -- @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) { throw "Segusum CLI failed with exit code $exitCode." }
    return $output
}

Write-Host "Generating AfterActionExecuted SEG from frozen source: $frozen"
Invoke-Cli @(
    'migrate-csharp', $frozen,
    '--world', 'game',
    '--context-root', $runtimeRoot,
    '--emit-partial',
    '--output', $tempSeg
) | Out-Null

if (-not (Test-Path -LiteralPath $tempSeg)) { throw "Generated SEG file not found: $tempSeg" }
if ((Select-String -LiteralPath $tempSeg -Pattern 'C2SEG-MANUAL' -SimpleMatch).Count -ne 0) { throw 'Generated SEG contains C2SEG-MANUAL markers.' }
Invoke-Cli @('parse-seg', $tempSeg) | Out-Null

if ($DryRun) {
    Write-Host "Ownership dry run"
    Invoke-Cli @('audit-ownership', $tempSeg, '--runtime-root', $runtimeRoot, '--history', $frozen) | Out-Null
    Write-Host "Dry run complete; runtime files were not changed."
    exit 0
}

Copy-Item -LiteralPath $tempSeg -Destination $runtimeSeg -Force
Write-Host "Updated runtime SEG: $runtimeSeg"

# Apply only the ownership discovered for this migration source.  The full
# runtime-root is used for the dry-run/reference audit above, but applying that
# report globally could remove declarations owned by unrelated SEG files.
if (Test-Path -LiteralPath $ownershipRoot) { Remove-Item -LiteralPath $ownershipRoot -Recurse -Force }
New-Item -ItemType Directory -Force $ownershipRoot | Out-Null
Get-ChildItem -LiteralPath $runtimeRoot -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    ForEach-Object {
        $relative = $_.FullName.Substring($runtimeRoot.Length).TrimStart('\\')
        $target = Join-Path $ownershipRoot $relative
        New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }

Invoke-Cli @(
    'audit-ownership', $tempSeg,
    '--runtime-root', $ownershipRoot,
    '--history', $frozen,
    '--methods-only',
    '--apply'
) | Out-Null

Get-ChildItem -LiteralPath $ownershipRoot -Filter '*.cs' -File -Recurse |
    ForEach-Object {
        $relative = $_.FullName.Substring($ownershipRoot.Length).TrimStart('\\')
        $destination = Join-Path $runtimeRoot $relative
        if (Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $_.FullName -Destination $destination -Force }
    }

Write-Host "Generated: $runtimeSeg"
Write-Host "C2SEG-MANUAL: 0"
