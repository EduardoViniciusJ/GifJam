# GifJam realtime contract

Endpoint: `/hubs/game`

Authentication: JWT bearer token supplied through SignalR's `accessTokenFactory`. Every command validates the authenticated user against the requested game.

## Client commands

| Command | Arguments | Result |
| --- | --- | --- |
| `SubscribeGame` | `gameCode` | Joins an authorized game group and emits `StateSynced` to the caller. |
| `RequestSync` | `gameCode` | Emits the caller's private snapshot, including completed actions. |
| `SetReady` | `gameCode`, `isReady` | Changes readiness while the game is in the lobby. |
| `UpdateGameSettings` | `gameCode`, `totalRounds`, `phraseSubmissionSeconds`, `resultsSeconds` | Changes lobby settings; host only. |
| `StartGame` | `gameCode` | Starts a ready lobby; host only. |
| `SubmitPhrase` | `gameCode`, `text` | Stores one phrase during `PhraseSubmission`. |
| `VotePhrase` | `gameCode`, `phraseId` | Stores one non-self phrase vote during `PhraseVoting`. |
| `SubmitGif` | `gameCode`, `selectionToken` | Stores or replaces a server-signed KLIPY or GIPHY selection during `GifSubmission`. |
| `VoteGif` | `gameCode`, `gifSubmissionId` | Stores one non-self GIF vote after the five-second-per-GIF presentation finishes. |
| `SetResultsReady` | `gameCode` | Confirms that the player finished viewing the reveal; all connected players advance early. |

## Server events

| Event | Payload | Audience |
| --- | --- | --- |
| `StateSynced` | `PlayerGameSnapshot` | Caller only; private `isOwn` and completed-action flags. |
| `LobbyUpdated` | `LobbySnapshot` | Game group. |
| `PresenceChanged` | `PresenceSnapshot` | Game group. |
| `PhaseChanged` | `RoundPhaseSnapshot` | Game group; phrase/GIF authors are omitted. |
| `SubmissionProgress` | `SubmissionProgressSnapshot` | Game group; counts only, never submitted content. |
| `RoundRevealed` | `RoundRevealSnapshot` | Game group after voting closes; includes authors and round positions. |
| `RankingUpdated` | `RankingSnapshot` | Game group after each reveal and at game completion. |
| `GameFinished` | `GameFinishedSnapshot` | Game group after the final results interval. |
| `CommandRejected` | `CommandRejectedMessage` | Caller only; stable error code and safe message. |

Deadlines are UTC timestamps supplied by the server. `GifVotingPresentationEndsAt` separates the synchronized presentation from the voting window. Clients render timers locally but never advance phases themselves.
