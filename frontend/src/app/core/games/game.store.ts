import { Injectable, computed, signal } from '@angular/core';

import { SessionUser } from '@core/auth/auth.models';

import {
  LobbySnapshot,
  PlayerGameSnapshot,
  PresenceSnapshot,
  SubmissionProgressSnapshot,
} from './game.models';

@Injectable()
export class GameStore {
  private readonly snapshotState = signal<PlayerGameSnapshot | null>(null);
  private readonly progressState = signal<SubmissionProgressSnapshot | null>(null);

  readonly snapshot = this.snapshotState.asReadonly();
  readonly lobby = computed(() => this.snapshotState()?.lobby ?? null);
  readonly round = computed(() => this.snapshotState()?.round ?? null);
  readonly submissionProgress = this.progressState.asReadonly();

  setSnapshot(snapshot: PlayerGameSnapshot): void {
    this.snapshotState.set(snapshot);
    this.progressState.set(null);
  }

  setLobby(lobby: LobbySnapshot): void {
    this.snapshotState.update((snapshot) =>
      snapshot ? { ...snapshot, lobby, isHost: snapshot.isHost } : null,
    );
  }

  setPresence(presence: PresenceSnapshot): void {
    const connectedByUser = new Map(
      presence.players.map((player) => [player.userId, player.isConnected]),
    );

    this.snapshotState.update((snapshot) => {
      if (!snapshot) {
        return null;
      }

      return {
        ...snapshot,
        lobby: {
          ...snapshot.lobby,
          serverTime: presence.serverTime,
          players: snapshot.lobby.players.map((player) => ({
            ...player,
            isConnected: connectedByUser.get(player.userId) ?? player.isConnected,
          })),
        },
      };
    });
  }

  setSubmissionProgress(progress: SubmissionProgressSnapshot): void {
    this.progressState.set(progress);
  }

  currentPlayer(user: SessionUser | null) {
    return computed(
      () => this.lobby()?.players.find((player) => player.userId === user?.id) ?? null,
    );
  }
}
