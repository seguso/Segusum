param(
    [Parameter(Mandatory = $true)]
    [string]$Project,
    [string]$WorkingDirectory = (Get-Location).Path,
    [int]$MemoryLimitGB = 4,
    [int]$NoProgressTimeoutSeconds = 60,
    [switch]$UseSharedCompilationFalse
)

$ErrorActionPreference = 'Stop'
$memoryLimitBytes = [int64]$MemoryLimitGB * 1GB
$buildStarted = Get-Date
$buildOutput = Join-Path $env:TEMP ("segusum-build-" + [guid]::NewGuid().ToString('N') + '.out.log')
$buildErrors = $buildOutput + '.err'

function Get-RoslynProcesses {
    @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('VBCSCompiler.exe', 'MSBuild.exe', 'dotnet.exe') -and
        ($_.CommandLine -match 'VBCSCompiler|MSBuild.dll|dotnet.exe.*build|dotnet.exe.*test')
    })
}

Write-Host "Roslyn guard: project=$Project limit=${MemoryLimitGB}GB no-progress=${NoProgressTimeoutSeconds}s"
$existing = Get-RoslynProcesses
foreach ($process in $existing) {
    try { $createdAt = [Management.ManagementDateTimeConverter]::ToDateTime($process.CreationDate); $age = ((Get-Date) - $createdAt).TotalMinutes }
    catch { $age = -1 }
    $workingSet = [int64]$process.WorkingSetSize
    Write-Host ("Existing {0} pid={1} parent={2} ageMin={3:0.0} workingSetMB={4}" -f $process.Name, $process.ProcessId, $process.ParentProcessId, $age, [math]::Round($workingSet / 1MB))
    if ($workingSet -ge $memoryLimitBytes) {
        throw "Anomalous existing Roslyn process pid=$($process.ProcessId) is above the memory limit. Stop it and retry."
    }
}

$arguments = @('build', $Project, '--verbosity', 'minimal')
if ($UseSharedCompilationFalse) { $arguments += '-p:UseSharedCompilation=false' }
$build = Start-Process -FilePath 'dotnet.exe' -ArgumentList $arguments -WorkingDirectory $WorkingDirectory -RedirectStandardOutput $buildOutput -RedirectStandardError $buildErrors -PassThru
$lastOutputLength = 0L
$lastProgress = Get-Date
$peak = @{}
$exitCode = 1

try {
    while (-not $build.HasExited) {
        Start-Sleep -Seconds 2
        foreach ($log in @($buildOutput, $buildErrors)) {
            if (Test-Path -LiteralPath $log) {
                $length = (Get-Item -LiteralPath $log).Length
                if ($length -gt $lastOutputLength) { $lastOutputLength = $length; $lastProgress = Get-Date }
            }
        }

        foreach ($process in (Get-RoslynProcesses)) {
            $workingSet = [int64]$process.WorkingSetSize
            if (-not $peak.ContainsKey($process.ProcessId) -or $workingSet -gt $peak[$process.ProcessId]) { $peak[$process.ProcessId] = $workingSet }
            if ($workingSet -ge $memoryLimitBytes) {
                Write-Warning "Memory guard triggered for $($process.Name) pid=$($process.ProcessId): $([math]::Round($workingSet / 1GB, 2)) GB"
                Stop-Process -Id $build.Id -Force -ErrorAction SilentlyContinue
                Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
                throw "Build aborted by Roslyn memory guard."
            }
        }

        if (((Get-Date) - $lastProgress).TotalSeconds -gt $NoProgressTimeoutSeconds) {
            Stop-Process -Id $build.Id -Force -ErrorAction SilentlyContinue
            throw "Build aborted after $NoProgressTimeoutSeconds seconds without output progress."
        }
    }
    $build.WaitForExit()
    $build.Refresh()
    $combinedOutput = ((Get-Content -LiteralPath $buildOutput -Raw -ErrorAction SilentlyContinue) + (Get-Content -LiteralPath $buildErrors -Raw -ErrorAction SilentlyContinue))
    $exitCode = if ($combinedOutput -match '(?m)^\s*Build succeeded\.') { 0 } else { 1 }
}
finally {
    $build.Refresh()
    $elapsed = ((Get-Date) - $buildStarted).TotalSeconds
    Write-Host "Build durationSec=$([math]::Round($elapsed, 1)) exit=$exitCode"
    foreach ($entry in $peak.GetEnumerator() | Sort-Object Value -Descending) {
        Write-Host "Peak pid=$($entry.Key) workingSetMB=$([math]::Round($entry.Value / 1MB))"
    }
    if (Test-Path -LiteralPath $buildOutput) { Get-Content -LiteralPath $buildOutput }
    if (Test-Path -LiteralPath $buildErrors) { Get-Content -LiteralPath $buildErrors }
    Remove-Item -LiteralPath $buildOutput, $buildErrors -Force -ErrorAction SilentlyContinue
}

exit $exitCode
