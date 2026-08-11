import { Injectable, computed, signal } from '@angular/core';

import { LobbySnapshot, PlayerGameSnapshot, PresenceSnapshot } from '../data/game.models';

@Injectable()
export class GameStore {
  private readonly snapshotState = signal<PlayerGameSnapshot | null>(null);

  readonly snapshot = this.snapshotState.asReadonly();
  readonly lobby = computed(() => this.snapshotState()?.lobby ?? null);
  readonly round = computed(() => this.snapshotState()?.round ?? null);

  setSnapshot(snapshot: PlayerGameSnapshot): void {
    this.snapshotState.set(snapshot);
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
}
