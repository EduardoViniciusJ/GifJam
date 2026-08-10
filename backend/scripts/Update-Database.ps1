$ErrorActionPreference = 'Stop'

$backendRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $backendRoot '.env'

. (Join-Path $PSScriptRoot 'Import-Environment.ps1')
Import-EnvironmentFile -Path $environmentFile

dotnet tool restore
dotnet tool run dotnet-ef database update `
    --project (Join-Path $backendRoot 'src/GifJam.Api/GifJam.Api.csproj') `
    --startup-project (Join-Path $backendRoot 'src/GifJam.Api/GifJam.Api.csproj')
