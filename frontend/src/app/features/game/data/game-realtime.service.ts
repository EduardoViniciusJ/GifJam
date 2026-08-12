import { Injectable, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import { environment } from '@env/environment';

import {
  CommandRejectedMessage,
  GameMode,
  LobbySnapshot,
  PlayerGameSnapshot,
  PresenceSnapshot,
} from './game.models';

export type RealtimeState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface GameRealtimeHandlers {
  stateSynced: (snapshot: PlayerGameSnapshot) => void;
  lobbyUpdated: (lobby: LobbySnapshot) => void;
  presenceChanged: (presence: PresenceSnapshot) => void;
  commandRejected: (rejection: CommandRejectedMessage) => void;
  gameStateChanged: () => void;
}

@Injectable()
export class GameRealtimeService {
  private connection: HubConnection | null = null;
  private gameCode = '';
  private connectionOperation: Promise<void> | null = null;
  private syncOperation: Promise<void> | null = null;
  private syncGameCode = '';
  private lastSyncGameCode = '';
  private lastSyncCompletedAt = 0;
  private gameStateChangeTimer: ReturnType<typeof setTimeout> | null = null;
  private pendingGameStateHandler: (() => void) | null = null;

  readonly state = signal<RealtimeState>('disconnected');
  readonly lastCommandRejected = signal<CommandRejectedMessage | null>(null);

  async connect(gameCode: string, handlers: GameRealtimeHandlers): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected && this.gameCode === gameCode) {
      return;
    }

    if (this.connectionOperation) {
      return this.connectionOperation;
    }

    const operation = this.connectInternal(gameCode, handlers);
    this.connectionOperation = operation;
    try {
      await operation;
    } finally {
      if (this.connectionOperation === operation) {
        this.connectionOperation = null;
      }
    }
  }

  private async connectInternal(gameCode: string, handlers: GameRealtimeHandlers): Promise<void> {
    await this.stop();
    this.gameCode = gameCode;
    this.lastCommandRejected.set(null);
    this.state.set('connecting');

    const connection = new HubConnectionBuilder()
      .withUrl(environment.gameHubUrl, { withCredentials: true })
      .withAutomaticReconnect([0, 1_000, 3_000, 5_000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('StateSynced', handlers.stateSynced);
    connection.on('LobbyUpdated', handlers.lobbyUpdated);
    connection.on('PresenceChanged', handlers.presenceChanged);
    connection.on('CommandRejected', (rejection) => {
      this.lastCommandRejected.set(rejection);
      handlers.commandRejected(rejection);
    });
    // A single command can publish several events (phase, reveal and ranking).
    // Coalesce the callbacks so one transition causes at most one snapshot
    // request instead of a burst of identical SignalR invocations.
    connection.on('PhaseChanged', () => this.queueGameStateChanged(handlers.gameStateChanged));
    connection.on('RoundRevealed', () => this.queueGameStateChanged(handlers.gameStateChanged));
    connection.on('RankingUpdated', () => this.queueGameStateChanged(handlers.gameStateChanged));
    connection.on('GameFinished', () => this.queueGameStateChanged(handlers.gameStateChanged));
    connection.onreconnecting(() => {
      if (this.connection === connection) {
        this.state.set('reconnecting');
      }
    });
    connection.onreconnected(async () => {
      if (this.connection !== connection) {
        return;
      }

      this.state.set('connected');
      try {
        await this.subscribe(connection, this.gameCode);
      } catch {
        this.state.set('reconnecting');
      }
    });
    connection.onclose(() => {
      if (this.connection === connection) {
        this.state.set('disconnected');
      }
    });

    this.connection = connection;
    await connection.start();
    if (this.connection !== connection) {
      await connection.stop();
      return;
    }

    this.state.set('connected');
    await this.subscribe(connection, gameCode);
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
    const now = Date.now();
    if (this.syncOperation && this.syncGameCode === gameCode) {
      return this.syncOperation;
    }

    // SignalR events are delivered just after the command that produced them.
    // A short cooldown prevents the event callback from immediately repeating
    // a snapshot that was already requested by the command path.
    if (this.lastSyncGameCode === gameCode && now - this.lastSyncCompletedAt < 250) {
      return Promise.resolve();
    }

    const operation = this.invoke('RequestSync', gameCode);
    this.syncOperation = operation;
    this.syncGameCode = gameCode;
    this.lastSyncGameCode = gameCode;
    operation.then(
      () => {
        if (this.syncOperation === operation) {
          this.lastSyncCompletedAt = Date.now();
        }
      },
      () => undefined,
    );
    return operation.finally(() => {
      if (this.syncOperation === operation) {
        this.syncOperation = null;
        this.syncGameCode = '';
      }
    });
  }

  clearCommandRejection(): void {
    this.lastCommandRejected.set(null);
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;

    if (this.gameStateChangeTimer) {
      clearTimeout(this.gameStateChangeTimer);
      this.gameStateChangeTimer = null;
    }
    this.pendingGameStateHandler = null;
    this.syncOperation = null;
    this.syncGameCode = '';
    this.lastSyncGameCode = '';
    this.lastSyncCompletedAt = 0;

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

  private async subscribe(connection: HubConnection, gameCode: string): Promise<void> {
    // SubscribeGame already sends StateSynced after validating membership and
    // updating presence. Avoid a second full database snapshot on every
    // initial connection and reconnect.
    await connection.invoke('SubscribeGame', gameCode);
  }

  private queueGameStateChanged(handler: () => void): void {
    this.pendingGameStateHandler = handler;
    if (this.gameStateChangeTimer) {
      return;
    }

    this.gameStateChangeTimer = setTimeout(() => {
      this.gameStateChangeTimer = null;
      const pendingHandler = this.pendingGameStateHandler;
      this.pendingGameStateHandler = null;
      pendingHandler?.();
    }, 50);
  }
}
