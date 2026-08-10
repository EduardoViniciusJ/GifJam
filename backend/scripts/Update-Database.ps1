param(
    [ValidateSet('Local', 'Neon')]
    [string]$Target = 'Local',

    [switch]$ConfirmNeon
)

$ErrorActionPreference = 'Stop'

$backendRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $backendRoot '.env'

$environmentVariableNames = Get-Content -LiteralPath $environmentFile |
    Where-Object { $_.Trim() -match '^[^#][^=]*=' } |
    ForEach-Object { ($_.Split('=', 2)[0]).Trim() } |
    Sort-Object -Unique
$previousValues = @{}

foreach ($name in $environmentVariableNames) {
    $previousValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    . (Join-Path $PSScriptRoot 'Import-Environment.ps1')
    Import-EnvironmentFile -Path $environmentFile

    if ($Target -eq 'Neon') {
        if (-not $ConfirmNeon) {
            throw 'Use -ConfirmNeon to acknowledge that migrations will change the remote Neon database.'
        }

        $neonConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__NeonDirect', 'Process')
        if ([string]::IsNullOrWhiteSpace($neonConnection) -or $neonConnection.StartsWith('replace_')) {
            throw 'ConnectionStrings__NeonDirect is missing from backend/.env.'
        }

        [Environment]::SetEnvironmentVariable('ConnectionStrings__Postgres', $neonConnection, 'Process')
    }

    Write-Output "Applying migrations to $Target database..."
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to restore .NET tools.'
    }

    dotnet tool run dotnet-ef database update `
        --project (Join-Path $backendRoot 'src/GifJam.Api/GifJam.Api.csproj') `
        --startup-project (Join-Path $backendRoot 'src/GifJam.Api/GifJam.Api.csproj')
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply migrations to $Target database."
    }
}
finally {
    foreach ($name in $environmentVariableNames) {
        [Environment]::SetEnvironmentVariable($name, $previousValues[$name], 'Process')
    }
}
