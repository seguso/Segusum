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
$tempRoot = Join-Path $env:TEMP 'segusum-litgir-after-action-executed-safe'
$tempSeg1 = Join-Path $tempRoot 'AfterActionExecuted.generated.1.seg'
$tempSeg2 = Join-Path $tempRoot 'AfterActionExecuted.generated.2.seg'
$cliProject = Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj'
$cliDll = Join-Path $segusumRoot 'Segusum.Migration.Cli\bin\Debug\net8.0\Segusum.Migration.Cli.dll'

if (-not (Test-Path -LiteralPath $frozen)) { throw "Frozen source not found: $frozen" }
if (-not (Test-Path -LiteralPath $cliProject)) { throw "CLI project not found: $cliProject" }
New-Item -ItemType Directory -Force $tempRoot | Out-Null

function Get-DescendantIds([int]$RootPid) {
    $all = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -in @('dotnet.exe','MSBuild.exe','VBCSCompiler.exe','csc.exe') })
    $ids = [System.Collections.Generic.HashSet[int]]::new()
    [void]$ids.Add($RootPid)
    $changed = $true
    while ($changed) {
        $changed = $false
        foreach ($child in $all) {
            if ($ids.Contains([int]$child.ParentProcessId) -and $ids.Add([int]$child.ProcessId)) { $changed = $true }
        }
    }
    return @($ids)
}

function Stop-Tree([int]$RootPid) {
    foreach ($childPid in (Get-DescendantIds $RootPid | Sort-Object -Descending)) { Stop-Process -Id $childPid -Force -ErrorAction SilentlyContinue }
}

function Invoke-Guarded([string]$FilePath, [string[]]$Arguments, [int]$TimeoutSeconds, [string]$Label) {
    $stdout = Join-Path $tempRoot ($Label + '.stdout.log')
    $stderr = Join-Path $tempRoot ($Label + '.stderr.log')
    Remove-Item -LiteralPath $stdout,$stderr -Force -ErrorAction SilentlyContinue
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $segusumRoot -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru -WindowStyle Hidden
    $peak = 0L
    try {
        while (-not $process.HasExited) {
            $working = 0L
            foreach ($childPid in (Get-DescendantIds $process.Id)) { try { $working += (Get-Process -Id $childPid -ErrorAction Stop).WorkingSet64 } catch { } }
            if ($working -gt $peak) { $peak = $working }
            if ($sw.Elapsed.TotalSeconds -ge $TimeoutSeconds) { Stop-Tree $process.Id; throw "TIMEOUT $Label after $([int]$sw.Elapsed.TotalSeconds)s; working-set=$([math]::Round($working/1MB,1))MB" }
            if ($working -gt 2GB) { Stop-Tree $process.Id; throw "MEMORY GUARD $Label at $([math]::Round($working/1MB,1))MB" }
            Start-Sleep -Milliseconds 250
            $process.Refresh()
        }
        $sw.Stop()
        $process.WaitForExit()
        $process.Refresh()
        $exitCode = [int]$process.ExitCode
        if (Test-Path $stdout) { Get-Content $stdout | ForEach-Object { Write-Host $_ } }
        if (Test-Path $stderr) { Get-Content $stderr | ForEach-Object { Write-Host $_ } }
        Write-Host ("GUARD {0}: elapsed={1:0.0}s peak-working-set={2:0.0}MB exit={3}" -f $Label,$sw.Elapsed.TotalSeconds,($peak/1MB),$exitCode)
        if ($exitCode -ne 0) { throw "Command failed: $Label exit=$exitCode" }
    } finally { if (-not $process.HasExited) { Stop-Tree $process.Id } }
}

function Invoke-Cli([string[]]$Arguments, [string]$Label) { Invoke-Guarded 'dotnet.exe' (@($cliDll) + $Arguments) 60 $Label }

Write-Host 'Building migration CLI once with compiler/node reuse disabled.'
Invoke-Guarded 'dotnet.exe' @('build',$cliProject,'-p:UseSharedCompilation=false','-m:1','/nodeReuse:false','--no-restore','-v:minimal') 120 'cli-build'
if (-not (Test-Path -LiteralPath $cliDll)) { throw "Built CLI not found: $cliDll" }

$arguments = @('migrate-csharp',$frozen,'--world','game','--context-root',$runtimeRoot,'--emit-partial','--output',$tempSeg1)
Write-Host "Generating from immutable frozen source: $frozen"
Invoke-Cli $arguments 'migrate-1'
$arguments2 = @('migrate-csharp',$frozen,'--world','game','--context-root',$runtimeRoot,'--emit-partial','--output',$tempSeg2)
Invoke-Cli $arguments2 'migrate-2'
if (-not (Test-Path $tempSeg1) -or -not (Test-Path $tempSeg2)) { throw 'Generation did not produce both temporary SEG files.' }
$h1 = (Get-FileHash $tempSeg1 -Algorithm SHA256).Hash
$h2 = (Get-FileHash $tempSeg2 -Algorithm SHA256).Hash
if ($h1 -ne $h2) { throw "Non-deterministic generation: $h1 vs $h2" }
Write-Host "generated-sha256: $h1"
if ($h1 -ne '38593978DD07179593F052FC1C89E313DE14D79E4A7C1CC1E1E0986A9F66147D') { throw "Generated artifact hash differs from the expected frozen-based baseline: $h1" }
if ((Select-String -LiteralPath $tempSeg1 -Pattern 'C2SEG-MANUAL' -SimpleMatch).Count -ne 0) { throw 'C2SEG-MANUAL found in generated SEG.' }
Invoke-Cli @('parse-seg',$tempSeg1) 'parse'
Write-Host 'Ownership dry-run only; no runtime file is changed.'
Invoke-Cli @('audit-ownership',$tempSeg1,'--runtime-root',$runtimeRoot,'--history',$frozen,'--methods-only') 'ownership-dry-run'
if ($DryRun) { exit 0 }
throw 'Refusing destructive apply from this safe workflow. Apply is intentionally disabled during recovery.'
