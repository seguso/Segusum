param([Parameter(Mandatory=$true)][string]$WorkingDirectory)
$ErrorActionPreference = 'Stop'
$stdout = Join-Path $env:TEMP 'codex-world-tranche-build.out'
$stderr = $stdout + '.err'
Remove-Item $stdout, $stderr -Force -ErrorAction SilentlyContinue
$argumentList = @('build','WebApiLitGir/WebApiLitGir.csproj','--no-restore','-p:UseSharedCompilation=false','-m:1','/nodeReuse:false','--verbosity','minimal')
$process = Start-Process dotnet -ArgumentList $argumentList -WorkingDirectory $WorkingDirectory -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
$timer = [Diagnostics.Stopwatch]::StartNew()
$peak = 0L
while (-not $process.HasExited) {
    Start-Sleep -Seconds 1
    $processes = @(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -eq $process.Id -or $_.ParentProcessId -eq $process.Id -or (($_.Name -in @('csc.exe','MSBuild.exe','VBCSCompiler.exe')) -and $_.CommandLine -match [regex]::Escape($WorkingDirectory)) })
    $workingSet = [int64](($processes | Measure-Object -Property WorkingSetSize -Sum).Sum)
    if ($workingSet -gt $peak) { $peak = $workingSet }
    if ($workingSet -ge 2GB) { taskkill /PID $process.Id /T /F | Out-Null; throw "MEMORY_GUARD $([math]::Round($workingSet / 1MB))MB" }
    if ($timer.Elapsed.TotalSeconds -gt 120) { taskkill /PID $process.Id /T /F | Out-Null; throw 'TIMEOUT' }
}
$process.WaitForExit()
$timer.Stop()
Get-Content $stdout -Raw -ErrorAction SilentlyContinue
Get-Content $stderr -Raw -ErrorAction SilentlyContinue
"elapsed=$([math]::Round($timer.Elapsed.TotalSeconds,1))s peak=$([math]::Round($peak / 1MB))MB exit=$($process.ExitCode)"
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
