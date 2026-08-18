# GifJam

GifJam is a multiplayer game. Players create phrases, choose GIFs, and vote for the best combination. Games happen in private rooms, with Discord login and real-time updates.

## Features

- Sign in with Discord OAuth2.
- Create a private room from Discord with `/gifjam-create`.
- Create private rooms, publish them in the public directory, or join by code.
- Play with 2 to 6 players.
- Use quick matchmaking to find a game.
- Create phrases manually or use AI-generated random phrases.
- Choose GIFs, vote, and see the ranking after each round.
- Search GIFs with KLIPY and GIPHY, with pagination for more results.
- Receive real-time game updates with SignalR.
- Reconnect and recover the current game state.
- View the global ranking, manage your profile, and delete your account.
- Hear music and sound effects during the game.

## Tech stack

- Backend: ASP.NET Core 10, Entity Framework Core, PostgreSQL, and SignalR.
- Frontend: Angular 22, TypeScript, Tailwind CSS, and Vitest.
- Authentication: Discord OAuth2 and JWT.
- GIF providers: KLIPY and GIPHY.

## Project structure

```text
backend/    API, business rules, database, SignalR, scripts, and external integrations
frontend/   Angular app and game interface
design/     Product design references and visual assets
```

## Requirements

- .NET SDK 10.0.302 or compatible.
- Node.js 22.22.3+ or 24.15.0+.
- npm 11 or newer.
- Docker Desktop, to run PostgreSQL locally.

## Local setup

1. Clone the repository and open the project folder.

2. Create the backend environment file:

   ```powershell
   Copy-Item backend/.env.example backend/.env
   ```

3. Fill in `backend/.env` with development credentials for Discord, KLIPY, GIPHY, Gemini, and JWT. Git ignores this file, so never commit it.

   To enable the Discord bot, follow [backend/contracts/discord-bot.md](backend/contracts/discord-bot.md). Use a development server ID locally so command updates appear immediately.

4. Start PostgreSQL:

   ```powershell
   cd backend
   docker compose up -d postgres
   ```

5. Apply database migrations:

   ```powershell
   ./scripts/Update-Database.ps1
   ```

## Run the app

Start the API in one terminal:

```powershell
./backend/scripts/Start-Api.ps1
```

The API runs at `https://localhost:7042`.

In another terminal, install frontend dependencies and start the app:

```powershell
cd frontend
npm ci
npm start
```

The app runs at `http://localhost:4200`. The Angular proxy sends REST and SignalR requests to the local API.

## Checks

Run frontend checks:

```powershell
cd frontend
npm run check
```

Run backend tests:

```powershell
cd backend
dotnet test
```

Integration tests that use PostgreSQL need Docker running.

## Deployment

- The frontend is prepared for Vercel.
- The API is prepared for a Docker deployment on a Linux VPS.
- PostgreSQL can run on Neon in production.
- The full deployment checklist is in [backend/contracts/deployment.md](backend/contracts/deployment.md).

Keep `.github`, `backend/contracts`, `backend/openapi`, and `backend/scripts` in Git. They contain CI, deployment, API contract, and operational files.

## Security

- Never commit API keys, OAuth secrets, passwords, or connection strings.
- KLIPY and GIPHY credentials are used only by the backend.
- Use `backend/.env.example` as the local configuration reference.
- In production, configure secrets in the hosting provider and review CORS and OAuth URLs.
