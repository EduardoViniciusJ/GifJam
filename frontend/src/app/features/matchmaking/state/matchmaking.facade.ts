import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '@core/auth/auth.service';
import { ApiProblemError } from '@core/models/problem-details.model';

import { MatchmakingApiService } from '../data/matchmaking-api.service';
import { MatchFoundSnapshot, MatchmakingSnapshot } from '../data/matchmaking.models';
import { MatchmakingRealtimeService } from '../data/matchmaking-realtime.service';

@Injectable()
export class MatchmakingFacade {
  private readonly auth = inject(AuthService);
  private readonly api = inject(MatchmakingApiService);
  private readonly realtime = inject(MatchmakingRealtimeService);
  private readonly router = inject(Router);

  readonly snapshot = signal<MatchmakingSnapshot | null>(null);
  readonly loading = signal(false);
  readonly message = signal('');
  readonly realtimeState = this.realtime.state;
  readonly isWaiting = computed(() => this.snapshot()?.status === 'Waiting');
  readonly queuePlayerLabel = computed(() => {
    const playerCount = this.snapshot()?.playerCount ?? 0;
    return playerCount === 1 ? '1 jogador na fila' : `${playerCount} jogadores na fila`;
  });
  readonly countdownSeconds = computed(() => {
    const deadlineAt = this.snapshot()?.deadlineAt;
    if (!this.isWaiting() || !deadlineAt) {
      return null;
    }

    const deadline = Date.parse(deadlineAt);
    if (!Number.isFinite(deadline)) {
      return null;
    }

    const serverNow = this.clientNow() + this.serverClockOffsetMs();
    return Math.max(0, Math.ceil((deadline - serverNow) / 1_000));
  });
  readonly queueTimingLabel = computed(() => {
    if (!this.isWaiting()) {
      return '';
    }

    if (!this.snapshot()?.deadlineAt) {
      return 'Aguardando outro jogador';
    }

    const seconds = this.countdownSeconds();
    return seconds === null || seconds === 0 ? 'Preparando partida...' : `Partida em ${seconds}s`;
  });

  private initialized = false;
  private readonly clientNow = signal(Date.now());
  private readonly serverClockOffsetMs = signal(0);
  private countdownTimer: ReturnType<typeof setInterval> | null = null;
  private recoveryOperation: Promise<void> | null = null;
  private lastRecoveryAttemptAt = 0;

  async initialize(): Promise<void> {
    if (!this.auth.isAuthenticated() || this.initialized) {
      return;
    }

    this.initialized = true;
    try {
      await this.refreshStatus();
      if (this.snapshot()?.status === 'Matched') {
        return;
      }

      await this.connectRealtime();
    } catch (error: unknown) {
      this.initialized = false;
      this.message.set(this.errorMessage(error));
    }
  }

  async toggleQueue(): Promise<void> {
    if (this.loading()) {
      return;
    }

    if (this.isWaiting()) {
      await this.leaveQueue();
      return;
    }

    await this.joinQueue();
  }

  async leaveQueue(): Promise<void> {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.message.set('');
    try {
      await firstValueFrom(this.api.leave());
      await this.refreshStatus();
      this.message.set('Você saiu da fila.');
    } catch (error: unknown) {
      this.message.set(this.errorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  async destroy(): Promise<void> {
    this.stopCountdown();
    await this.realtime.stop();
  }

  private async joinQueue(): Promise<void> {
    this.loading.set(true);
    this.message.set('');
    try {
      await this.connectRealtime();
      const current = await firstValueFrom(this.api.join());
      this.setSnapshot(current);
    } catch (error: unknown) {
      this.message.set(this.errorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  private async refreshStatus(): Promise<void> {
    this.setSnapshot(await firstValueFrom(this.api.status()));
  }

  private async connectRealtime(): Promise<void> {
    if (this.realtimeState() === 'connected') {
      return;
    }

    await this.realtime.connect({
      updated: (snapshot) => this.setSnapshot(snapshot),
      found: (snapshot) => this.handleMatchFound(snapshot),
    });
  }

  private setSnapshot(snapshot: MatchmakingSnapshot): void {
    const receivedAt = Date.now();
    const serverTime = Date.parse(snapshot.serverTime);
    this.clientNow.set(receivedAt);
    if (Number.isFinite(serverTime)) {
      this.serverClockOffsetMs.set(serverTime - receivedAt);
    }

    this.snapshot.set(snapshot);
    if (snapshot.status === 'Waiting') {
      this.message.set('Aguardando jogadores...');
    }

    if (snapshot.status === 'Waiting' && snapshot.deadlineAt) {
      this.startCountdown();
    } else {
      this.stopCountdown();
    }

    if (snapshot.status === 'Matched' && snapshot.gameCode) {
      this.message.set('Partida encontrada. Entrando na sala...');
      void this.router.navigate(['/sala', snapshot.gameCode]);
    }
  }

  private handleMatchFound(snapshot: MatchFoundSnapshot): void {
    this.setSnapshot({
      status: 'Matched',
      playerCount: snapshot.playerCount,
      minimumPlayers: snapshot.playerCount,
      maximumPlayers: snapshot.playerCount,
      hostUserId: snapshot.hostUserId,
      deadlineAt: null,
      gameCode: snapshot.gameCode,
      gameMode: 'Classic',
      serverTime: snapshot.serverTime,
    });
  }

  private startCountdown(): void {
    if (this.countdownTimer) {
      return;
    }

    this.countdownTimer = setInterval(() => {
      this.clientNow.set(Date.now());
      if (this.countdownSeconds() === 0) {
        void this.recoverCompletedMatch();
      }
    }, 1_000);
  }

  private stopCountdown(): void {
    if (!this.countdownTimer) {
      return;
    }

    clearInterval(this.countdownTimer);
    this.countdownTimer = null;
  }

  private async recoverCompletedMatch(): Promise<void> {
    if (this.recoveryOperation || !this.isWaiting()) {
      return;
    }

    const now = Date.now();
    if (now - this.lastRecoveryAttemptAt < 3_000) {
      return;
    }

    this.lastRecoveryAttemptAt = now;

    const operation = this.refreshStatus();
    this.recoveryOperation = operation;
    try {
      await operation;
    } catch {
      // The timer retries while the match remains unresolved.
    } finally {
      if (this.recoveryOperation === operation) {
        this.recoveryOperation = null;
      }
    }
  }

  private errorMessage(error: unknown): string {
    if (error instanceof ApiProblemError) {
      if (error.problem.code === 'already_in_game') {
        return 'Você já está em uma partida. Saia dela antes de entrar na fila.';
      }

      return error.message;
    }

    return 'Não foi possível acessar a fila agora. Tente novamente.';
  }
}
