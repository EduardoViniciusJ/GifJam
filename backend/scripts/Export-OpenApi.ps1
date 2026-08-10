param(
    [string]$OutputPath = "openapi/openapi.json",
    [int]$Port = 5099
)

$ErrorActionPreference = "Stop"
$backendRoot = Split-Path -Parent $PSScriptRoot
$defaults = @{
    "ASPNETCORE_ENVIRONMENT" = "Development"
    "BackgroundServices__Enabled" = "false"
    "ConnectionStrings__Postgres" = "Host=127.0.0.1;Port=1;Database=gifjam_openapi;Username=gifjam;Password=gifjam_openapi;Timeout=1;Command Timeout=1"
    "Discord__ClientId" = "openapi-client"
    "Discord__ClientSecret" = "openapi-secret"
    "Discord__CallbackUrl" = "https://localhost/api/auth/discord/callback"
    "Klipy__ApiKey" = "openapi-klipy-key"
    "Jwt__SigningKey" = ("o" * 64)
    "ApplicationUrls__FrontendUrl" = "https://frontend.example"
}
$environmentKeys = @($defaults.Keys) + "ASPNETCORE_URLS"
$originalEnvironment = @{}
foreach ($key in $environmentKeys) {
    $originalEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, "Process")
}

foreach ($entry in $defaults.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, "Process")
}

$url = "http://127.0.0.1:$Port"
[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", $url, "Process")
& dotnet build (Join-Path $backendRoot "GifJam.sln") --no-restore | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "The API could not be built for OpenAPI export."
}

$assemblyPath = Join-Path $backendRoot "src/GifJam.Api/bin/Debug/net10.0/GifJam.Api.dll"
$standardOutput = [IO.Path]::GetTempFileName()
$standardError = [IO.Path]::GetTempFileName()
$startParameters = @{
    FilePath = "dotnet"
    ArgumentList = @($assemblyPath, "--urls", $url)
    WorkingDirectory = $backendRoot
    PassThru = $true
    RedirectStandardOutput = $standardOutput
    RedirectStandardError = $standardError
}
if ($IsWindows) {
    $startParameters["WindowStyle"] = "Hidden"
}

$process = Start-Process @startParameters
try {
    $openApiUrl = "$url/swagger/v1/swagger.json"
    $content = $null
    Add-Type -AssemblyName System.Net.Http
    $httpClient = [System.Net.Http.HttpClient]::new()
    $httpClient.Timeout = [TimeSpan]::FromSeconds(2)
    for ($attempt = 0; $attempt -lt 120 -and $null -eq $content; $attempt++) {
        try {
            $content = $httpClient.GetStringAsync($openApiUrl).GetAwaiter().GetResult()
        }
        catch {
            if ($process.HasExited) {
                Get-Content -LiteralPath $standardOutput -ErrorAction SilentlyContinue | Select-Object -Last 20 | Out-Host
                Get-Content -LiteralPath $standardError -ErrorAction SilentlyContinue | Select-Object -Last 20 | Out-Host
                throw "The API exited before OpenAPI could be exported."
            }

            Start-Sleep -Milliseconds 250
        }
    }

    $httpClient.Dispose()
    if ($null -eq $content) {
        Get-Content -LiteralPath $standardOutput -ErrorAction SilentlyContinue | Select-Object -Last 20 | Out-Host
        Get-Content -LiteralPath $standardError -ErrorAction SilentlyContinue | Select-Object -Last 20 | Out-Host
        throw "Timed out waiting for $openApiUrl."
    }

    $absoluteOutputPath = Join-Path $backendRoot $OutputPath
    $outputDirectory = Split-Path -Parent $absoluteOutputPath
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    [IO.File]::WriteAllText($absoluteOutputPath, $content, [Text.UTF8Encoding]::new($false))
    Write-Output "OpenAPI exported to $absoluteOutputPath"
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }

    Remove-Item -LiteralPath $standardOutput, $standardError -Force -ErrorAction SilentlyContinue
    foreach ($key in $environmentKeys) {
        [Environment]::SetEnvironmentVariable($key, $originalEnvironment[$key], "Process")
    }
}
