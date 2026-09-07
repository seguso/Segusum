$ErrorActionPreference = 'Stop'
$segusumRoot = Split-Path -Parent $PSScriptRoot
$litgirRoot = Join-Path (Split-Path -Parent $segusumRoot) 'litgir'
$demoRoot = Join-Path $env:TEMP 'segusum-litgir-demo'
$source = Join-Path $demoRoot 'worldOnRoomChanged.cs'
$liveSource = Join-Path $litgirRoot 'WebApiLitGir/worldOnRoomChanged.cs'
$migrationSource = Join-Path $litgirRoot 'docs/migration-inputs/worldOnRoomChanged.cs'
$worldObjects = Join-Path $demoRoot 'worldObjects.cs'
$output = Join-Path $env:TEMP 'worldOnRoomChanged.generated.seg'

New-Item -ItemType Directory -Force $demoRoot | Out-Null
if (Test-Path -LiteralPath $liveSource) {
    $inputSource = $liveSource
} elseif (Test-Path -LiteralPath $migrationSource) {
    $inputSource = $migrationSource
} else {
    throw "C# migration source not found. Expected either '$liveSource' or '$migrationSource'."
}
Copy-Item -LiteralPath $inputSource -Destination $source -Force
Copy-Item (Join-Path $litgirRoot 'WebApiLitGir/worldObjects.cs') $worldObjects -Force
Write-Host "Source:"
Write-Host $inputSource
$cliArgs = @(
    'run', '--project', (Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj'),
    '--no-restore', '--', 'migrate-csharp', $source, '--output', $output, '--emit-partial'
)
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$cliOutput = & dotnet @cliArgs 2>&1
$cliExit = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
$cliOutput | ForEach-Object { Write-Host $_ }
if ($cliExit -gt 1) { throw "migration CLI failed with exit code $cliExit" }
Write-Host "Generated:"
Write-Host $output
exit 0
