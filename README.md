<p align="center">
  <img src="frontend/public/brand/gifjam-app-icon.png" alt="GifJam icon" width="128" height="128" />
</p>

<h1 align="center">GifJam</h1>

<p align="center">
  <strong>The right phrase. The perfect GIF.</strong><br />
  Online party game for 2 to 6 friends: write phrases, pick GIFs, and vote — in the browser, with Discord login.
</p>

<p align="center">
  <a href="https://gifjam.com.br">Play at gifjam.com.br</a>
</p>

<p align="center">
  <a href="#how-to-play">How to play</a> ·
  <a href="#features">Features</a> ·
  <a href="#tech-stack">Tech stack</a> ·
  <a href="#getting-started">Getting started</a> ·
  <a href="https://github.com/EduardoViniciusJ/GifJam/issues">Report a bug or suggest an idea</a>
</p>

> [!NOTE]
> GifJam is free and still in development. It is a short party game for Discord calls: everyone writes a phrase, the group votes on one, each player answers with a GIF, then you vote for the funniest combo. Rooms are private (or listed in the public directory), updates are real-time, and you play in the browser — no install.

## How to play

1. **Phrase** — each player submits a phrase (anonymous).
2. **Phrase vote** — the group picks the round’s prompt.
3. **GIF** — everyone searches and sends a GIF as their answer.
4. **GIF vote** — one vote for your favorite, without knowing the author.
5. **Reveal** — authors, points, and the match ranking.

A 3-round match usually fits in about 15 minutes. The host chooses 3 to 6 rounds.

## Features

- **Login with Discord:** name and avatar you already use in the call — no extra account.
- **Private rooms:** create a room, share a 5-character code or link, and start when everyone is ready (2–6 players).
- **Discord bot:** `/gifjam-create` opens a room from the server; `/gifjam-room` republishes the code and link.
- **Public directory and quick matchmaking:** list a room or jump into a match without passing a code around.
- **Anonymous rounds:** phrases and GIFs stay unnamed until the reveal, so votes stay unbiased.
- **GIF search:** KLIPY and GIPHY, with pagination — search in Portuguese, pick, vote.
- **AI prompt option:** generate a random phrase with Gemini instead of typing one.
- **Real-time sync:** SignalR keeps lobby, timers, votes and ranking in lockstep; reconnect and recover the current phase.
- **Global ranking, profile and account deletion:** see who is on top, manage your profile, or wipe the account.
- **Music and SFX:** in-game soundtrack and sound effects you can mute from the UI.
- **Play on phone or desktop:** responsive web app, no native client.

## Discord commands

| Command | Action |
|---|---|
| `/gifjam-create` | Create a private room (or republish the one you already host) |
| `/gifjam-room` | Post the current room code and link in the channel |
| `/gifjam-close` | Close the lobby you are hosting |
| `/gifjam-help` | Show the command list (ephemeral) |

## Tech stack

| Layer | Technologies |
|---|---|
| Frontend | Angular 22 · TypeScript · Tailwind CSS · Spartan UI · Vitest |
| Backend | ASP.NET Core 10 · Entity Framework Core · SignalR · PostgreSQL |
| Auth | Discord OAuth2 · JWT |
| GIF search | KLIPY · GIPHY |
| Optional AI | Google Gemini (`gemini-3.5-flash`) |
| Discord | Slash commands via Discord.Net |
| Deploy | Docker · Traefik · Vercel-ready frontend · Neon-ready Postgres |
| Code quality | TypeScript / Angular ESLint · Prettier · `dotnet format` · GitHub Actions |

## Integrations

External services the API talks to (keys stay on the backend — never in the Angular bundle):

| Service | Purpose |
|---|---|
| [Discord](https://discord.com/developers/docs/topics/oauth2) | Login (OAuth2) and optional room-creation bot |
| [KLIPY](https://klipy.com) | Primary GIF search |
| [GIPHY](https://developers.giphy.com) | Additional GIF results |
| [Gemini](https://ai.google.dev) | Optional random phrases during a round |

GIF media is loaded from the provider CDN in the browser. Search credentials never leave the API.

## Getting started

The easiest way to play is [gifjam.com.br](https://gifjam.com.br).

To run from source you need **.NET SDK 10.0.302** (or compatible), **Node.js 22.22.3+ or 24.15.0+**, **npm 11+**, and **Docker Desktop** for PostgreSQL. Then:

```powershell
git clone https://github.com/EduardoViniciusJ/GifJam.git
cd GifJam
Copy-Item backend/.env.example backend/.env
```

Fill `backend/.env` with development credentials for Discord, KLIPY, GIPHY, Gemini and JWT. Git ignores this file — never commit it.

To enable the Discord bot locally, follow [backend/contracts/discord-bot.md](backend/contracts/discord-bot.md). Use a development server ID so slash-command updates appear immediately.

```powershell
cd backend
docker compose up -d postgres
./scripts/Update-Database.ps1
```

Start the API in one terminal:

```powershell
./backend/scripts/Start-Api.ps1
```

The API listens at `https://localhost:7042`.

In another terminal:

```powershell
cd frontend
npm ci
npm start
```

The app runs at `http://localhost:4200`. The Angular proxy forwards REST and SignalR to the local API.

<details>
<summary>Development commands</summary>

From the project root:

```powershell
cd backend
dotnet test
dotnet format GifJam.sln --verify-no-changes
```

```powershell
cd frontend
npm run check
```

`npm run check` runs Prettier, ESLint, Vitest and a production build. Backend integration tests that hit PostgreSQL need Docker running.

</details>

## Project structure

```text
backend/    API, game engine, PostgreSQL, SignalR, Discord bot, scripts
frontend/   Angular app (public brand assets in frontend/public/brand)
design/     Product design references and wireframes
```

Keep `.github`, `backend/contracts`, `backend/openapi` and `backend/scripts` in Git. They hold CI, deployment, API contract and ops files.

## Deployment

- Frontend is prepared for Vercel (or the Docker image behind Traefik at `gifjam.com.br`).
- API is prepared for Docker on a Linux VPS.
- PostgreSQL can run on Neon in production.
- Full checklist: [backend/contracts/deployment.md](backend/contracts/deployment.md).

## Your data

Discord identity (display name, avatar) is used to play. Game state lives in PostgreSQL for the active match and is cleaned up after retention (24 hours by default). You can delete your account from the profile. GIF search keys and OAuth secrets stay on the server.

GifJam is an independent project and is not affiliated with Discord, KLIPY or GIPHY.

## Security

- Never commit API keys, OAuth secrets, passwords or connection strings.
- Use `backend/.env.example` as the local configuration reference.
- In production, configure secrets in the host and review CORS and OAuth callback URLs.
