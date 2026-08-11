# Backend deployment runbook

## Target

- Hostinger Linux VPS running the existing Docker image.
- One VPS and one API container for the MVP because game locks are process-local.
- Neon Free as the PostgreSQL provider.
- Vercel as the frontend provider.
- HTTPS required for Discord OAuth and SignalR.

## Preconditions

1. Rotate every credential previously shared in chat, screenshots, or committed files.
2. Create a production Discord application, a production KLIPY key, and request the appropriate GIPHY production access for the planned traffic.
3. Configure the KLIPY Partner Panel content filter and attribution requirements.
4. Create the Hostinger VPS with Ubuntu 24.04 LTS and Docker.
5. Register `gifjam.com.br` and point its DNS record to the VPS public IP.
6. Confirm the final Vercel frontend origin before configuring CORS and Discord OAuth.

## Database

Keep PostgreSQL on Neon for the first VPS deployment. Do not expose port `5432` publicly and do not use the local development PostgreSQL service in production.

Apply migrations through the direct Neon endpoint. The command requires an explicit remote confirmation and restores process environment variables when it finishes.

```powershell
.\scripts\Update-Database.ps1 -Target Neon -ConfirmNeon
```

The running API receives `ConnectionStrings__Neon`, which should use the pooled Neon endpoint.

## Hostinger VPS deployment checklist

1. Copy the VPS public IP from the Hostinger panel.
2. Allow inbound TCP 22 only for administration, and allow TCP 80 and 443 for web traffic.
3. Keep application port `8080` and PostgreSQL port `5432` private.
4. Connect with the configured SSH key.
5. Install Docker Engine and the Docker Compose plugin if the VPS template did not install them.
6. Clone the repository into a server directory.
7. Create a server-side production `.env`; never commit it.
8. Build the API image from `backend/src/GifJam.Api/Dockerfile`.
9. Run one API container with restart enabled and bind it to `127.0.0.1:8080`.
10. Put Caddy or Nginx in front of the container and issue an HTTPS certificate for `gifjam.com.br`.
11. Keep one API replica until distributed locking is introduced.

The production environment should contain `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`, the Neon connection strings, the production Discord callback, the frontend origin, and the server-side API keys, including `Klipy__ApiKey` and `Giphy__ApiKey`. Do not copy local `POSTGRES_*` values into production when using Neon.

## Smoke test

```powershell
.\scripts\Smoke-Backend.ps1 -BaseUrl 'https://gifjam.com.br'
.\scripts\Smoke-Backend.ps1 -BaseUrl 'https://gifjam.com.br' -AccessToken '<jwt>'
```

Before publishing, the same public smoke test can run against the final Docker image and local PostgreSQL without using external Discord or KLIPY secrets:

```powershell
docker build -f src/GifJam.Api/Dockerfile -t gifjam-api:stage10 .
.\scripts\Test-Container.ps1
```

## Final checks

- Confirm the Discord redirect URL matches exactly: `https://gifjam.com.br/api/auth/discord/callback`.
- Confirm CORS accepts only `https://gif-jam.vercel.app` until a custom frontend domain is configured.
- Confirm Neon migrations completed successfully.
- Verify SignalR negotiation and WebSocket connection over HTTPS.
- Verify records older than 24 hours are removed by the cleanup worker.
- Check VPS CPU, memory, disk, and container restart status.
