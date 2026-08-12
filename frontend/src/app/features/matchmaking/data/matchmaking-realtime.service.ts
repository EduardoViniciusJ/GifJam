import { Injectable, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import { environment } from '@env/environment';

import { MatchFoundSnapshot, MatchmakingSnapshot } from './matchmaking.models';

export type MatchmakingRealtimeState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface MatchmakingRealtimeHandlers {
  updated: (snapshot: MatchmakingSnapshot) => void;
  found: (snapshot: MatchFoundSnapshot) => void;
}

@Injectable()
export class MatchmakingRealtimeService {
  private connection: HubConnection | null = null;
  private connectionOperation: Promise<void> | null = null;

  readonly state = signal<MatchmakingRealtimeState>('disconnected');

  async connect(handlers: MatchmakingRealtimeHandlers): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    if (this.connectionOperation) {
      return this.connectionOperation;
    }

    const operation = this.connectInternal(handlers);
    this.connectionOperation = operation;
    try {
      await operation;
    } finally {
      if (this.connectionOperation === operation) {
        this.connectionOperation = null;
      }
    }
  }

  private async connectInternal(handlers: MatchmakingRealtimeHandlers): Promise<void> {
    await this.stop();
    this.state.set('connecting');

    const connection = new HubConnectionBuilder()
      .withUrl(environment.gameHubUrl, {
        withCredentials: true,
      })
      .withAutomaticReconnect([0, 1_000, 3_000, 5_000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('MatchmakingUpdated', handlers.updated);
    connection.on('MatchFound', handlers.found);
    connection.onreconnecting(() => {
      if (this.connection === connection) {
        this.state.set('reconnecting');
      }
    });
    connection.onreconnected(() => {
      if (this.connection === connection) {
        this.state.set('connected');
      }
    });
    connection.onclose(() => {
      if (this.connection === connection) {
        this.state.set('disconnected');
      }
    });

    this.connection = connection;
    try {
      await connection.start();
      if (this.connection !== connection) {
        await connection.stop();
        return;
      }

      this.state.set('connected');
    } catch (error) {
      this.connection = null;
      this.state.set('disconnected');
      throw error;
    }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;

    if (connection && connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }

    this.state.set('disconnected');
  }
}
