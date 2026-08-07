$ErrorActionPreference = 'Stop'

$backendRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $backendRoot '.env'

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw "Missing $environmentFile. Create it from .env.example first."
}

foreach ($line in Get-Content -LiteralPath $environmentFile) {
    $trimmedLine = $line.Trim()
    if ($trimmedLine.Length -eq 0 -or $trimmedLine.StartsWith('#')) {
        continue
    }

    $separatorIndex = $trimmedLine.IndexOf('=')
    if ($separatorIndex -le 0) {
        continue
    }

    $name = $trimmedLine.Substring(0, $separatorIndex)
    $value = $trimmedLine.Substring($separatorIndex + 1)
    [Environment]::SetEnvironmentVariable($name, $value, 'Process')
}

dotnet run --project (Join-Path $backendRoot 'src/GifJam.Api/GifJam.Api.csproj') --launch-profile https
