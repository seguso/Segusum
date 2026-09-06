$ErrorActionPreference = 'Stop'
$segusumRoot = Split-Path -Parent $PSScriptRoot
$litgirRoot = Join-Path (Split-Path -Parent $segusumRoot) 'litgir'
$demoRoot = Join-Path $env:TEMP 'segusum-litgir-demo'
$source = Join-Path $demoRoot 'worldOnRoomChanged.cs'
$worldObjects = Join-Path $demoRoot 'worldObjects.cs'
$output = Join-Path $env:TEMP 'worldOnRoomChanged.generated.seg'

New-Item -ItemType Directory -Force $demoRoot | Out-Null
$git = [Diagnostics.ProcessStartInfo]::new()
$git.FileName = 'git'
$git.Arguments = "-C `"$litgirRoot`" show 35509be2d:WebApiLitGir/worldOnRoomChanged.cs"
$git.RedirectStandardOutput = $true
$git.RedirectStandardError = $true
$git.UseShellExecute = $false
$git.StandardOutputEncoding = [Text.Encoding]::UTF8
$process = [Diagnostics.Process]::Start($git)
$sourceText = $process.StandardOutput.ReadToEnd()
$sourceError = $process.StandardError.ReadToEnd()
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "git show failed: $sourceError" }
[IO.File]::WriteAllText($source, $sourceText, [Text.UTF8Encoding]::new($false))
Copy-Item (Join-Path $litgirRoot 'WebApiLitGir/worldObjects.cs') $worldObjects -Force
dotnet run --project (Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj') --no-restore -- migrate-csharp $source --output $output --emit-partial
Write-Host "Generated:"
Write-Host $output
