import { Location } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '@core/auth/auth.service';
import { ApiProblemError } from '@core/models/problem-details.model';
import { GameApiService } from '@features/game/data/game-api.service';
import {
  CommandRejectedMessage,
  GameMode,
  GlobalRankingSnapshot,
  RoomVisibility,
} from '@features/game/data/game.models';
import { GameRealtimeService } from '@features/game/data/game-realtime.service';
import { GameStore } from '@features/game/state/game.store';
import { RankingApiService } from '@features/ranking/data/ranking-api.service';

export type RoomPageStatus = 'loading' | 'ready' | 'error';

@Injectable()
export class RoomFacade {
  readonly auth = inject(AuthService);
  readonly realtime = inject(GameRealtimeService);

  private readonly location = inject(Location);
  private readonly router = inject(Router);
  private readonly gameApi = inject(GameApiService);
  private readonly rankingApi = inject(RankingApiService);
  private readonly store = inject(GameStore);

  readonly lobby = this.store.lobby;
  readonly status = signal<RoomPageStatus>('loading');
  readonly loadingMessage = signal('Preparando a conexão em tempo real.');
  readonly errorMessage = signal('Não foi possível carregar a sala.');
  readonly actionMessage = signal('');
  readonly actionPending = signal(false);
  readonly leavingRoom = signal(false);
  readonly copied = signal(false);
  readonly globalRanking = signal<GlobalRankingSnapshot | null>(null);
  readonly globalRankingStatus = signal<'loading' | 'ready' | 'error'>('loading');
  readonly currentPlayer = computed(() => {
    const userId = this.auth.user()?.id;
    return this.lobby()?.players.find((player) => player.userId === userId) ?? null;
  });
  readonly connectionLabel = computed(() => {
    switch (this.realtime.state()) {
      case 'connected':
        return 'Conectado';
      case 'reconnecting':
        return 'Reconectando';
      case 'connecting':
        return 'Conectando';
      default:
        return 'Desconectado';
    }
  });

  private initializePromise: Promise<void> | null = null;
  private copiedTimer: ReturnType<typeof setTimeout> | null = null;
  private actionMessageTimer: ReturnType<typeof setTimeout> | null = null;

  initialize(requestedCode: string): Promise<void> {
    if (!this.isValidRoomCode(requestedCode)) {
      this.showError('O código da sala deve ter exatamente cinco caracteres.');
      return Promise.resolve();
    }

    if (this.initializePromise) {
      return this.initializePromise;
    }

    const operation = this.loadRoom(requestedCode);
    this.initializePromise = operation;

    return operation.finally(() => {
      if (this.initializePromise === operation) {
        this.initializePromise = null;
      }
    });
  }

  retry(requestedCode: string): Promise<void> {
    this.status.set('loading');
    this.errorMessage.set('Não foi possível carregar a sala.');
    return this.initialize(requestedCode);
  }

  async toggleReady(): Promise<void> {
    const player = this.currentPlayer();
    const code = this.lobby()?.code;
    if (!player || !code || player.isHost) {
      return;
    }

    await this.runCommand(() => this.realtime.setReady(code, !player.isReady));
  }

  async setVisibility(visibility: RoomVisibility): Promise<void> {
    const code = this.lobby()?.code;
    if (!code || !this.currentPlayer()?.isHost || this.actionPending()) {
      return;
    }

    await this.runCommand(() => this.realtime.setRoomVisibility(code, visibility));
  }

  async updateSettings(
    totalRounds: number,
    phraseSubmissionSeconds: number,
    resultsSeconds: number,
    mode: GameMode,
  ): Promise<void> {
    const code = this.lobby()?.code;
    if (!code || !this.currentPlayer()?.isHost || this.actionPending()) {
      return;
    }

    await this.runCommand(() =>
      this.realtime.updateGameSettingsWithMode(
        code,
        totalRounds,
        phraseSubmissionSeconds,
        resultsSeconds,
        mode,
      ),
    );
  }

  async startGame(): Promise<void> {
    const code = this.lobby()?.code;
    if (!code) {
      return;
    }

    await this.runCommand(() => this.realtime.startGame(code));
  }

  async leaveRoom(): Promise<void> {
    const code = this.lobby()?.code;
    if (!code || this.leavingRoom()) {
      return;
    }

    this.leavingRoom.set(true);
    this.dismissActionMessage();
    try {
      await firstValueFrom(this.gameApi.leave(code));
      await this.realtime.stop();
      await this.router.navigateByUrl('/');
    } catch (error: unknown) {
      if (error instanceof ApiProblemError && error.status === 404) {
        await this.realtime.stop();
        await this.router.navigateByUrl('/');
        return;
      }

      this.showActionMessage('Não foi possível sair da sala. Tente novamente.');
    } finally {
      this.leavingRoom.set(false);
    }
  }

  async copyInvite(): Promise<void> {
    const code = this.lobby()?.code;
    if (!code) {
      return;
    }

    const inviteUrl = `${window.location.origin}/sala/${code}`;
    try {
      if (!navigator.clipboard) {
        throw new Error('Clipboard unavailable');
      }

      await navigator.clipboard.writeText(inviteUrl);
      this.copied.set(true);
      if (this.copiedTimer) {
        clearTimeout(this.copiedTimer);
      }
      this.copiedTimer = setTimeout(() => this.copied.set(false), 2_000);
    } catch {
      this.showActionMessage('Não foi possível copiar o link. Compartilhe o código da sala.');
    }
  }

  async destroy(): Promise<void> {
    if (this.copiedTimer) {
      clearTimeout(this.copiedTimer);
      this.copiedTimer = null;
    }

    if (this.actionMessageTimer) {
      clearTimeout(this.actionMessageTimer);
      this.actionMessageTimer = null;
    }

    await this.realtime.stop();
  }

  dismissActionMessage(): void {
    if (this.actionMessageTimer) {
      clearTimeout(this.actionMessageTimer);
      this.actionMessageTimer = null;
    }

    this.actionMessage.set('');
  }

  private async loadRoom(requestedCode: string): Promise<void> {
    try {
      const user = await firstValueFrom(this.auth.restore());
      if (!user) {
        this.auth.startDiscordLogin(`/sala/${requestedCode.toLowerCase()}`);
        return;
      }

      void this.loadGlobalRanking();

      this.loadingMessage.set(
        requestedCode === 'NOVA' ? 'Criando uma nova sala.' : 'Entrando na sala.',
      );
      const snapshot = await firstValueFrom(
        requestedCode === 'NOVA'
          ? this.gameApi.create({
              totalRounds: 3,
              phraseSubmissionSeconds: 60,
              resultsSeconds: 60,
              mode: 'Classic',
            })
          : this.gameApi.join(requestedCode),
      );

      this.store.setSnapshot(snapshot);
      if (requestedCode === 'NOVA') {
        this.location.replaceState(`/sala/${snapshot.lobby.code}`);
      }

      await this.realtime.connect(snapshot.lobby.code, {
        stateSynced: (synced) => this.store.setSnapshot(synced),
        lobbyUpdated: (lobby) => this.store.setLobby(lobby),
        presenceChanged: (presence) => this.store.setPresence(presence),
        commandRejected: (rejection) => this.handleCommandRejected(rejection),
        gameStateChanged: () => void this.syncGame(snapshot.lobby.code),
      });
      this.status.set('ready');
    } catch (error: unknown) {
      this.handleLoadError(error, requestedCode);
    }
  }

  private async runCommand(command: () => Promise<void>): Promise<void> {
    if (this.actionPending()) {
      return;
    }

    this.actionPending.set(true);
    this.dismissActionMessage();
    try {
      await command();
    } catch {
      this.showActionMessage('A ação não foi enviada. A conexão será sincronizada novamente.');
      await this.syncCurrentGame();
    } finally {
      this.actionPending.set(false);
    }
  }

  private async loadGlobalRanking(): Promise<void> {
    this.globalRankingStatus.set('loading');
    try {
      this.globalRanking.set(await firstValueFrom(this.rankingApi.getGlobal()));
      this.globalRankingStatus.set('ready');
    } catch {
      this.globalRanking.set(null);
      this.globalRankingStatus.set('error');
    }
  }

  private handleCommandRejected(rejection: CommandRejectedMessage): void {
    this.showActionMessage(commandErrorMessage(rejection.code, rejection.message));

    // RequestSync is itself rate limited. Retrying it after a rate-limit
    // rejection would create a rejection/sync loop in the client.
    if (rejection.code !== 'rate_limited') {
      void this.syncCurrentGame();
    }
  }

  private async syncCurrentGame(): Promise<void> {
    const code = this.lobby()?.code;
    if (code) {
      await this.syncGame(code);
    }
  }

  private async syncGame(code: string): Promise<void> {
    if (this.realtime.state() !== 'connected') {
      return;
    }

    try {
      await this.realtime.requestSync(code);
    } catch {
      this.showActionMessage('O jogo será sincronizado quando a conexão voltar.');
    }
  }

  private showActionMessage(message: string): void {
    if (this.actionMessageTimer) {
      clearTimeout(this.actionMessageTimer);
    }

    this.actionMessage.set(message);
    this.actionMessageTimer = setTimeout(() => {
      this.actionMessage.set('');
      this.actionMessageTimer = null;
    }, 6_000);
  }

  private handleLoadError(error: unknown, requestedCode: string): void {
    if (error instanceof ApiProblemError) {
      if (error.status === 401) {
        this.auth.logout();
        this.auth.startDiscordLogin(`/sala/${requestedCode.toLowerCase()}`);
        return;
      }

      this.showError(gameErrorMessage(error.problem.code));
      return;
    }

    this.showError('Não foi possível conectar ao servidor. Verifique se o backend está ativo.');
  }

  private showError(message: string): void {
    this.errorMessage.set(message);
    this.status.set('error');
  }

  private isValidRoomCode(code: string): boolean {
    return code === 'NOVA' || /^[A-Z0-9]{5}$/.test(code);
  }
}

function gameErrorMessage(code?: string): string {
  switch (code) {
    case 'game_not_found':
      return 'A sala não existe ou já foi encerrada.';
    case 'game_full':
      return 'A sala já está com seis jogadores.';
    case 'game_already_started':
      return 'A partida já começou e não aceita novos jogadores.';
    default:
      return 'Não foi possível criar ou entrar nesta sala.';
  }
}

function commandErrorMessage(code: string, serverMessage: string): string {
  switch (code) {
    case 'rate_limited':
      return 'Você está fazendo ações rápido demais. Tente novamente em instantes.';
    case 'lobby_not_ready':
    case 'not_all_players_ready':
      return 'Todos os jogadores precisam estar prontos.';
    case 'host_required':
      return 'Somente o host pode iniciar a partida.';
    case 'phase_expired':
      return 'Essa etapa terminou. O estado da sala foi atualizado.';
    default:
      return (
        serverMessage || 'Essa ação não está disponível agora. O estado da sala foi atualizado.'
      );
  }
}
