# Backend deployment runbook

## Current target

- Azure Container Apps in `brazilsouth`.
- One active revision and at most one replica because game locks are process-local.
- Scale to zero outside active HTTP/SignalR traffic.
- Azure Container Registry with administrative credentials disabled.
- User-assigned managed identity with only the `AcrPull` role.
- Neon pooled connection for the API and direct connection only for migrations.

## Preconditions

1. Rotate every credential previously shared in chat or screenshots.
2. Create a production Discord application and a production KLIPY key.
3. Configure the KLIPY Partner Panel content filter and attribution requirements.
4. Install Azure CLI, run `az login`, and select a subscription with billing enabled.
5. Choose the final HTTPS frontend origin and, if used, the custom API domain.

## Database

Apply migrations through the direct Neon endpoint. The command requires an explicit remote confirmation and restores process environment variables when it finishes.

```powershell
.\scripts\Update-Database.ps1 -Target Neon -ConfirmNeon
```

The running API receives `ConnectionStrings__Neon`, which should use the pooled Neon endpoint.

## Azure

The deployment script creates an ACR Basic registry, builds the API image remotely, creates the Container Apps environment, and deploys the application. Secret values are loaded from the ignored `backend/.env`, passed as secure ARM parameters through a temporary file, and stored as Container Apps secrets.

```powershell
.\scripts\Deploy-Azure.ps1 `
  -SubscriptionId '<subscription-id>' `
  -RegistryName '<globally-unique-acr-name>' `
  -FrontendUrl 'https://app.example.com' `
  -ConfirmProductionCredentials
```

Without `-DiscordCallbackUrl`, Bicep derives the callback from the generated Container Apps domain and the script prints the exact URL to register in Discord. Pass the parameter only when a custom API domain already exists.

`minReplicas` is `0`, `maxReplicas` is `1`, insecure ingress is disabled, and startup/liveness/readiness probes use `/health/live` and `/health/ready`.

## Smoke test

Run public checks first. After completing Discord OAuth, pass a short-lived JWT to verify the authenticated profile, room creation, SignalR negotiation, and WebSocket handshake.

```powershell
.\scripts\Smoke-Backend.ps1 -BaseUrl 'https://<container-app-fqdn>'
.\scripts\Smoke-Backend.ps1 -BaseUrl 'https://<container-app-fqdn>' -AccessToken '<jwt>'
```

Before publishing, the same public smoke test can run against the final Docker image and local PostgreSQL without using external Discord or KLIPY secrets:

```powershell
docker build -f src/GifJam.Api/Dockerfile -t gifjam-api:stage10 .
.\scripts\Test-Container.ps1
```

Complete one three-round game with 2, 3, and 6 browser sessions. During `GifSubmission`, confirm that search returns real KLIPY results and attribution without exposing the API key. Automated integration tests exercise the same player counts and scoring rules before deployment.

## Final checks

- Confirm the Discord redirect URL matches exactly.
- Confirm CORS accepts only the production frontend origin.
- Inspect Container Apps logs for redacted secrets and trace IDs.
- Verify records older than 24 hours are removed by the cleanup worker.
- Keep a single API replica until distributed locking is introduced.
