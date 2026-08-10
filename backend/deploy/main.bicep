targetScope = 'resourceGroup'

param location string = resourceGroup().location
param appName string = 'gifjam-api-hml'
param environmentName string = 'gifjam-hml'
param identityName string = 'gifjam-api-pull-hml'
param registryName string
param containerImage string
param frontendUrl string
param discordClientId string
param discordCallbackUrl string = ''

@secure()
param postgresConnectionString string

@secure()
param discordClientSecret string

@secure()
param klipyApiKey string

@secure()
param jwtSigningKey string

var registryServer = '${registryName}.azurecr.io'
var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
}

resource imagePullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, imagePullIdentity.id, acrPullRoleDefinitionId)
  scope: registry
  properties: {
    principalId: imagePullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: acrPullRoleDefinitionId
  }
}

resource environment 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: environmentName
  location: location
  properties: {}
}

var generatedApiHost = '${appName}.${environment.properties.defaultDomain}'
var effectiveDiscordCallbackUrl = empty(discordCallbackUrl)
  ? 'https://${generatedApiHost}/api/auth/discord/callback'
  : discordCallbackUrl

resource api 'Microsoft.App/containerApps@2025-07-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${imagePullIdentity.id}': {}
    }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        transport: 'auto'
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: registryServer
          identity: imagePullIdentity.id
        }
      ]
      secrets: [
        {
          name: 'postgres-connection'
          value: postgresConnectionString
        }
        {
          name: 'discord-client-secret'
          value: discordClientSecret
        }
        {
          name: 'klipy-api-key'
          value: klipyApiKey
        }
        {
          name: 'jwt-signing-key'
          value: jwtSigningKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: containerImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Staging'
            }
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: '8080'
            }
            {
              name: 'ConnectionStrings__Postgres'
              secretRef: 'postgres-connection'
            }
            {
              name: 'Discord__ClientId'
              value: discordClientId
            }
            {
              name: 'Discord__ClientSecret'
              secretRef: 'discord-client-secret'
            }
            {
              name: 'Discord__CallbackUrl'
              value: effectiveDiscordCallbackUrl
            }
            {
              name: 'Klipy__ApiKey'
              secretRef: 'klipy-api-key'
            }
            {
              name: 'Jwt__SigningKey'
              secretRef: 'jwt-signing-key'
            }
            {
              name: 'ApplicationUrls__FrontendUrl'
              value: frontendUrl
            }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 1
              periodSeconds: 2
              timeoutSeconds: 2
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 15
              timeoutSeconds: 3
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 3
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 6
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
        rules: [
          {
            name: 'http-requests'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    acrPull
  ]
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output discordCallbackUrl string = effectiveDiscordCallbackUrl
