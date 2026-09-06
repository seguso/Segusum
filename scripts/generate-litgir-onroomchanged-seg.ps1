$ErrorActionPreference = 'Stop'
$segusumRoot = Split-Path -Parent $PSScriptRoot
$litgirRoot = Join-Path (Split-Path -Parent $segusumRoot) 'litgir'
$demoRoot = Join-Path $env:TEMP 'segusum-litgir-demo'
$source = Join-Path $demoRoot 'worldOnRoomChanged.cs'
$worldObjects = Join-Path $demoRoot 'worldObjects.cs'
$output = Join-Path $env:TEMP 'worldOnRoomChanged.generated.seg'

New-Item -ItemType Directory -Force $demoRoot | Out-Null
git -C $litgirRoot show '35509be2d:WebApiLitGir/worldOnRoomChanged.cs' | Set-Content -Encoding utf8 $source
Copy-Item (Join-Path $litgirRoot 'WebApiLitGir/worldObjects.cs') $worldObjects -Force
dotnet run --project (Join-Path $segusumRoot 'Segusum.Migration.Cli\Segusum.Migration.Cli.csproj') --no-restore -- migrate-csharp $source --output $output --emit-partial
Write-Host "Generated:"
Write-Host $output
