import { Injectable, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import { SessionTokenService } from '@core/auth/session-token.service';
import { environment } from '@env/environment';

import {
  CommandRejectedMessage,
  GameMode,
  LobbySnapshot,
  PlayerGameSnapshot,
  PresenceSnapshot,
  SubmissionProgressSnapshot,
} from './game.models';

export type RealtimeState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface GameRealtimeHandlers {
  stateSynced: (snapshot: PlayerGameSnapshot) => void;
  lobbyUpdated: (lobby: LobbySnapshot) => void;
  presenceChanged: (presence: PresenceSnapshot) => void;
  submissionProgressChanged: (progress: SubmissionProgressSnapshot) => void;
  commandRejected: (rejection: CommandRejectedMessage) => void;
  gameStateChanged: () => void;
}

@Injectable()
export class GameRealtimeService {
  private readonly session = inject(SessionTokenService);
  private connection: HubConnection | null = null;
  private gameCode = '';

  readonly state = signal<RealtimeState>('disconnected');
  readonly lastCommandRejected = signal<CommandRejectedMessage | null>(null);

  async connect(gameCode: string, handlers: GameRealtimeHandlers): Promise<void> {
    await this.stop();
    this.gameCode = gameCode;
    this.lastCommandRejected.set(null);
    this.state.set('connecting');

    const connection = new HubConnectionBuilder()
      .withUrl(environment.gameHubUrl, { accessTokenFactory: () => this.session.get() ?? '' })
      .withAutomaticReconnect([0, 1_000, 3_000, 5_000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('StateSynced', handlers.stateSynced);
    connection.on('LobbyUpdated', handlers.lobbyUpdated);
    connection.on('PresenceChanged', handlers.presenceChanged);
    connection.on('SubmissionProgress', handlers.submissionProgressChanged);
    connection.on('CommandRejected', (rejection) => {
      this.lastCommandRejected.set(rejection);
      handlers.commandRejected(rejection);
    });
    connection.on('PhaseChanged', handlers.gameStateChanged);
    connection.on('SubmissionProgress', handlers.gameStateChanged);
    connection.on('RoundRevealed', handlers.gameStateChanged);
    connection.on('RankingUpdated', handlers.gameStateChanged);
    connection.on('GameFinished', handlers.gameStateChanged);
    connection.onreconnecting(() => this.state.set('reconnecting'));
    connection.onreconnected(async () => {
      this.state.set('connected');
      await connection.invoke('SubscribeGame', this.gameCode);
    });
    connection.onclose(() => this.state.set('disconnected'));

    this.connection = connection;
    await connection.start();
    this.state.set('connected');
    await connection.invoke('SubscribeGame', gameCode);
  }

  setReady(gameCode: string, isReady: boolean): Promise<void> {
    return this.invoke('SetReady', gameCode, isReady);
  }

  updateGameSettings(
    gameCode: string,
    totalRounds: number,
    phraseSubmissionSeconds: number,
    resultsSeconds: number,
  ): Promise<void> {
    return this.invoke(
      'UpdateGameSettings',
      gameCode,
      totalRounds,
      phraseSubmissionSeconds,
      resultsSeconds,
    );
  }

  updateGameSettingsWithMode(
    gameCode: string,
    totalRounds: number,
    phraseSubmissionSeconds: number,
    resultsSeconds: number,
    mode: GameMode,
  ): Promise<void> {
    return this.invoke(
      'UpdateGameSettingsWithMode',
      gameCode,
      totalRounds,
      phraseSubmissionSeconds,
      resultsSeconds,
      mode,
    );
  }

  startGame(gameCode: string): Promise<void> {
    return this.invoke('StartGame', gameCode);
  }

  submitPhrase(gameCode: string, text: string): Promise<void> {
    return this.invoke('SubmitPhrase', gameCode, text);
  }

  votePhrase(gameCode: string, phraseId: string): Promise<void> {
    return this.invoke('VotePhrase', gameCode, phraseId);
  }

  submitGif(gameCode: string, selectionToken: string): Promise<void> {
    return this.invoke('SubmitGif', gameCode, selectionToken);
  }

  voteGif(gameCode: string, gifId: string): Promise<void> {
    return this.invoke('VoteGif', gameCode, gifId);
  }

  setResultsReady(gameCode: string): Promise<void> {
    return this.invoke('SetResultsReady', gameCode);
  }

  requestSync(gameCode: string): Promise<void> {
    return this.invoke('RequestSync', gameCode);
  }

  clearCommandRejection(): void {
    this.lastCommandRejected.set(null);
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;

    if (connection && connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }

    this.state.set('disconnected');
  }

  private async invoke(method: string, ...args: unknown[]): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      throw new Error('Realtime connection is not ready.');
    }

    await this.connection.invoke(method, ...args);
  }
}
