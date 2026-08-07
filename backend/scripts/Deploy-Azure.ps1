param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9]{5,50}$')]
    [string]$RegistryName,

    [Parameter(Mandatory = $true)]
    [uri]$FrontendUrl,

    [uri]$DiscordCallbackUrl,

    [string]$ResourceGroup = 'rg-gifjam-hml',
    [string]$Location = 'brazilsouth',
    [string]$AppName = 'gifjam-api-hml',
    [string]$EnvironmentName = 'gifjam-hml',
    [string]$ImageTag = 'latest',

    [switch]$ConfirmProductionCredentials
)

$ErrorActionPreference = 'Stop'

if (-not $ConfirmProductionCredentials) {
    throw 'Use -ConfirmProductionCredentials only after rotating exposed credentials and configuring production Discord/KLIPY applications.'
}

if ($FrontendUrl.Scheme -ne 'https' -or
    ($null -ne $DiscordCallbackUrl -and $DiscordCallbackUrl.Scheme -ne 'https')) {
    throw 'FrontendUrl and any explicit DiscordCallbackUrl must use HTTPS.'
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI is required. Install it with: winget install --exact --id Microsoft.AzureCLI'
}

$backendRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $backendRoot '.env'
$deploymentRoot = Join-Path $backendRoot 'deploy'
$temporaryParametersFile = Join-Path ([System.IO.Path]::GetTempPath()) "gifjam-deploy-$([guid]::NewGuid()).json"
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

    $requiredSecrets = @{
        postgresConnectionString = [Environment]::GetEnvironmentVariable('ConnectionStrings__Neon', 'Process')
        discordClientId = [Environment]::GetEnvironmentVariable('Discord__ClientId', 'Process')
        discordClientSecret = [Environment]::GetEnvironmentVariable('Discord__ClientSecret', 'Process')
        klipyApiKey = [Environment]::GetEnvironmentVariable('Klipy__ApiKey', 'Process')
        jwtSigningKey = [Environment]::GetEnvironmentVariable('Jwt__SigningKey', 'Process')
    }

    foreach ($entry in $requiredSecrets.GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace($entry.Value) -or $entry.Value.StartsWith('replace_')) {
            throw "Missing deployment value: $($entry.Key)."
        }
    }

    if ($requiredSecrets.jwtSigningKey.Length -lt 64) {
        throw 'Jwt__SigningKey must contain at least 64 characters.'
    }

    az account set --subscription $SubscriptionId
    if ($LASTEXITCODE -ne 0) {
        throw 'Azure login or subscription selection failed. Run az login first.'
    }

    az extension add --name containerapp --upgrade --only-show-errors
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to install or update the Azure Container Apps extension.'
    }
    az provider register --namespace Microsoft.App --wait
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to register Microsoft.App.'
    }
    az provider register --namespace Microsoft.ContainerRegistry --wait
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to register Microsoft.ContainerRegistry.'
    }
    az provider register --namespace Microsoft.ManagedIdentity --wait
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to register Microsoft.ManagedIdentity.'
    }

    az group create --name $ResourceGroup --location $Location --output none
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to create or update the Azure resource group.'
    }
    az deployment group create `
        --resource-group $ResourceGroup `
        --template-file (Join-Path $deploymentRoot 'registry.bicep') `
        --parameters registryName=$RegistryName location=$Location `
        --output none
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to deploy Azure Container Registry.'
    }

    $containerImage = "$RegistryName.azurecr.io/gifjam-api:$ImageTag"
    az acr build `
        --registry $RegistryName `
        --image "gifjam-api:$ImageTag" `
        --file (Join-Path $backendRoot 'src/GifJam.Api/Dockerfile') `
        $backendRoot `
        --output none
    if ($LASTEXITCODE -ne 0) {
        throw 'Azure Container Registry build failed.'
    }

    $parameters = @{
        '$schema' = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
        contentVersion = '1.0.0.0'
        parameters = @{
            location = @{ value = $Location }
            appName = @{ value = $AppName }
            environmentName = @{ value = $EnvironmentName }
            identityName = @{ value = "$AppName-pull" }
            registryName = @{ value = $RegistryName }
            containerImage = @{ value = $containerImage }
            frontendUrl = @{ value = $FrontendUrl.AbsoluteUri.TrimEnd('/') }
            discordClientId = @{ value = $requiredSecrets.discordClientId }
            discordCallbackUrl = @{
                value = if ($null -eq $DiscordCallbackUrl) { '' } else { $DiscordCallbackUrl.AbsoluteUri }
            }
            postgresConnectionString = @{ value = $requiredSecrets.postgresConnectionString }
            discordClientSecret = @{ value = $requiredSecrets.discordClientSecret }
            klipyApiKey = @{ value = $requiredSecrets.klipyApiKey }
            jwtSigningKey = @{ value = $requiredSecrets.jwtSigningKey }
        }
    }
    $parameters | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporaryParametersFile -Encoding utf8

    $deployment = az deployment group create `
        --resource-group $ResourceGroup `
        --template-file (Join-Path $deploymentRoot 'main.bicep') `
        --parameters "@$temporaryParametersFile" `
        --query properties.outputs `
        --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw 'Azure Container Apps deployment failed.'
    }

    Write-Output "API deployed to $($deployment.apiUrl.value)"
    Write-Output "Register this exact Discord redirect URL: $($deployment.discordCallbackUrl.value)"
}
finally {
    if (Test-Path -LiteralPath $temporaryParametersFile) {
        Remove-Item -LiteralPath $temporaryParametersFile -Force
    }

    foreach ($name in $environmentVariableNames) {
        [Environment]::SetEnvironmentVariable($name, $previousValues[$name], 'Process')
    }
}
