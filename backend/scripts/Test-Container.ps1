param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 5080
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$backendRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $backendRoot '.env'
$temporaryEnvironmentFile = Join-Path ([System.IO.Path]::GetTempPath()) "gifjam-container-$([guid]::NewGuid()).env"
$containerName = "gifjam-stage10-$([guid]::NewGuid().ToString('N').Substring(0, 8))"

if (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue) {
    throw "Port $Port is already in use."
}

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

    $connectionString = [Environment]::GetEnvironmentVariable('ConnectionStrings__Postgres', 'Process')
    $localPassword = [Environment]::GetEnvironmentVariable('POSTGRES_PASSWORD', 'Process')
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw 'ConnectionStrings__Postgres is missing from backend/.env.'
    }

    $containerConnectionString = $connectionString -replace '(?i)Host=localhost', 'Host=host.docker.internal'
    $environmentLines = @(
        'ASPNETCORE_ENVIRONMENT=Staging'
        'ASPNETCORE_HTTP_PORTS=8080'
        "ConnectionStrings__Postgres=$containerConnectionString"
        'Discord__ClientId=container-smoke-client'
        'Discord__ClientSecret=container-smoke-secret'
        "Discord__CallbackUrl=http://127.0.0.1:$Port/api/auth/discord/callback"
        'Klipy__ApiKey=container-smoke-key'
        'Giphy__ApiKey=container-smoke-giphy-key'
        "Jwt__SigningKey=$('x' * 64)"
        'ApplicationUrls__FrontendUrl=http://localhost:4200'
    )
    [System.IO.File]::WriteAllLines(
        $temporaryEnvironmentFile,
        $environmentLines,
        [System.Text.UTF8Encoding]::new($false))

    try {
        docker run `
            --detach `
            --name $containerName `
            --env-file $temporaryEnvironmentFile `
            --publish "127.0.0.1:${Port}:8080" `
            gifjam-api:stage10 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to start the API container.'
        }

        $ready = $false
        $healthHandler = [System.Net.Http.HttpClientHandler]::new()
        $healthHandler.UseProxy = $false
        $healthClient = [System.Net.Http.HttpClient]::new($healthHandler)
        $healthClient.Timeout = [TimeSpan]::FromSeconds(2)
        try {
            for ($attempt = 0; $attempt -lt 20; $attempt++) {
                $isRunning = docker inspect --format '{{.State.Running}}' $containerName 2>$null
                if ($isRunning -ne 'true') {
                    break
                }

                try {
                    $response = $healthClient.GetAsync("http://127.0.0.1:$Port/health/ready").GetAwaiter().GetResult()
                    if ($response.IsSuccessStatusCode) {
                        $ready = $true
                        break
                    }
                }
                catch {
                    Start-Sleep -Milliseconds 500
                }
            }
        }
        finally {
            $healthClient.Dispose()
            $healthHandler.Dispose()
        }

        if (-not $ready) {
            $previousErrorActionPreference = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try {
                $failureLogs = (docker logs --tail 30 $containerName 2>&1 | Out-String)
            }
            finally {
                $ErrorActionPreference = $previousErrorActionPreference
            }
            if (-not [string]::IsNullOrEmpty($localPassword)) {
                $failureLogs = $failureLogs.Replace($localPassword, '[redacted]')
            }
            $failureLogs = $failureLogs.Replace('container-smoke-secret', '[redacted]').Replace(
                'container-smoke-key',
                '[redacted]')
            throw "The API container did not become ready. Sanitized logs:`n$failureLogs"
        }

        & (Join-Path $PSScriptRoot 'Smoke-Backend.ps1') -BaseUrl "http://127.0.0.1:$Port"
        $containerLogs = docker logs $containerName 2>&1 | Out-String
        if (-not [string]::IsNullOrEmpty($localPassword) -and $containerLogs.Contains($localPassword)) {
            throw 'The local database password appeared in container logs.'
        }
        if ($containerLogs -match '(?i)(container-smoke-secret|container-smoke-key|Jwt__SigningKey)') {
            throw 'A configured secret appeared in container logs.'
        }

        Write-Output 'Container logs passed the secret scan.'
    }
    finally {
        docker rm --force $containerName 2>$null | Out-Null
        if (Test-Path -LiteralPath $temporaryEnvironmentFile) {
            Remove-Item -LiteralPath $temporaryEnvironmentFile -Force
        }
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryEnvironmentFile) {
        Remove-Item -LiteralPath $temporaryEnvironmentFile -Force
    }

    foreach ($name in $environmentVariableNames) {
        [Environment]::SetEnvironmentVariable($name, $previousValues[$name], 'Process')
    }
}
