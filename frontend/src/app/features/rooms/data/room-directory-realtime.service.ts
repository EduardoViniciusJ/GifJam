import { Injectable } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import { environment } from '@env/environment';

@Injectable()
export class RoomDirectoryRealtimeService {
  private connection: HubConnection | null = null;
  private connectionOperation: Promise<void> | null = null;

  async connect(directoryChanged: () => void): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    if (this.connectionOperation) {
      return this.connectionOperation;
    }

    const operation = this.connectInternal(directoryChanged);
    this.connectionOperation = operation;
    try {
      await operation;
    } finally {
      if (this.connectionOperation === operation) {
        this.connectionOperation = null;
      }
    }
  }

  private async connectInternal(directoryChanged: () => void): Promise<void> {
    await this.stop();
    const connection = new HubConnectionBuilder()
      .withUrl(environment.roomDirectoryHubUrl, { withCredentials: true })
      .withAutomaticReconnect([0, 1_000, 3_000, 5_000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('DirectoryChanged', directoryChanged);
    connection.onreconnected(directoryChanged);
    connection.onclose(() => {
      if (this.connection === connection) {
        this.connection = null;
      }
    });

    this.connection = connection;
    try {
      await connection.start();
    } catch (error) {
      if (this.connection === connection) {
        this.connection = null;
      }
      throw error;
    }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    if (connection && connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }
  }
}
