import { Location } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideCheck,
  lucideClock,
  lucideCopy,
  lucideCrown,
  lucideLink,
  lucidePlay,
  lucideRefreshCw,
  lucideSparkles,
  lucideUsers,
  lucideWifi,
  lucideWifiOff,
} from '@ng-icons/lucide';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '@core/auth/auth.service';
import { GameApiService } from '@core/games/game-api.service';
import { GameRealtimeService } from '@core/games/game-realtime.service';
import { GameStore } from '@core/games/game.store';
import { CommandRejectedMessage, GameMode } from '@core/games/game.models';
import { ApiProblemError } from '@core/models/problem-details.model';
import { BrandComponent } from '@shared/ui/brand/brand.component';
import { GamePhasePage } from '@features/game/game-phase.page';

type RoomPageStatus = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-room-page',
  imports: [BrandComponent, GamePhasePage, NgIcon, RouterLink],
  providers: [
    GameStore,
    GameRealtimeService,
    provideIcons({
      lucideArrowLeft,
      lucideCheck,
      lucideClock,
      lucideCopy,
      lucideCrown,
      lucideLink,
      lucidePlay,
      lucideRefreshCw,
      lucideSparkles,
      lucideUsers,
      lucideWifi,
      lucideWifiOff,
    }),
  ],
  template: `
    <div class="game-shell">
      <header class="game-header">
        <app-brand tone="dark" />
        @if (auth.user(); as user) {
          <div class="game-user">
            @if (user.avatarUrl) {
              <img [src]="user.avatarUrl" width="40" height="40" alt="" />
            } @else {
              <span class="avatar-fallback">{{ user.displayName.charAt(0) }}</span>
            }
            <span>{{ user.displayName }}</span>
          </div>
        }
      </header>

      @if (status() === 'loading') {
        <main class="route-state" aria-live="polite">
          <span class="route-state__icon route-state__icon--orange">
            <ng-icon name="lucideUsers" aria-hidden="true" />
          </span>
          <p class="eyebrow eyebrow--dark">SALA</p>
          <h1>{{ requestedCode === 'NOVA' ? 'Criando sua sala' : requestedCode }}</h1>
          <p class="route-state__message">
            <ng-icon name="lucideClock" aria-hidden="true" />
            {{ loadingMessage() }}
          </p>
        </main>
      } @else if (status() === 'error') {
        <main class="route-state" aria-live="assertive">
          <span class="route-state__icon route-state__icon--orange">
            <ng-icon name="lucideWifiOff" aria-hidden="true" />
          </span>
          <p class="eyebrow eyebrow--dark">NÃO FOI POSSÍVEL ENTRAR</p>
          <h1>Esta sala não está disponível</h1>
          <p>{{ errorMessage() }}</p>
          <div class="route-state__actions">
            <button class="button button--primary" type="button" (click)="retry()">
              <ng-icon name="lucideRefreshCw" aria-hidden="true" />
              Tentar novamente
            </button>
            <a class="button button--outline" routerLink="/">
              <ng-icon name="lucideArrowLeft" aria-hidden="true" />
              Voltar ao início
            </a>
          </div>
        </main>
      } @else if (lobby(); as lobby) {
        @if (lobby.status === 'Lobby') {
          <main class="lobby-page">
            <section class="lobby-heading" aria-labelledby="lobby-title">
              <span class="lobby-heading__icon">
                <ng-icon name="lucideCrown" aria-hidden="true" />
              </span>
              <div>
                <p class="eyebrow eyebrow--dark">SALA</p>
                <h1 id="lobby-title">{{ lobby.code }}</h1>
                <div class="lobby-heading__meta">
                  <button class="button button--outline" type="button" (click)="copyInvite()">
                    <ng-icon [name]="copied() ? 'lucideCheck' : 'lucideLink'" aria-hidden="true" />
                    {{ copied() ? 'Link copiado' : 'Copiar link' }}
                  </button>
                  <span>
                    <ng-icon name="lucideUsers" aria-hidden="true" />
                    {{ lobby.players.length }}
                    {{ lobby.players.length === 1 ? 'jogador' : 'jogadores' }}
                  </span>
                  <span
                    class="connection-label"
                    [class.connection-label--online]="realtime.state() === 'connected'"
                  >
                    <ng-icon
                      [name]="realtime.state() === 'connected' ? 'lucideWifi' : 'lucideWifiOff'"
                      aria-hidden="true"
                    />
                    {{ connectionLabel() }}
                  </span>
                </div>
              </div>
            </section>

            <section class="player-list" aria-label="Jogadores na sala">
              @for (player of lobby.players; track player.userId) {
                <article class="lobby-player" [class.lobby-player--offline]="!player.isConnected">
                  <div class="lobby-player__avatar">
                    @if (player.avatarUrl) {
                      <img [src]="player.avatarUrl" width="96" height="96" alt="" />
                    } @else {
                      <span>{{ player.displayName.charAt(0) }}</span>
                    }
                    <i
                      [class]="
                        player.isConnected ? 'presence-dot' : 'presence-dot presence-dot--off'
                      "
                    ></i>
                  </div>
                  <div class="lobby-player__identity">
                    <strong>{{ player.displayName }}</strong>
                    @if (player.isHost) {
                      <span class="host-badge">
                        <ng-icon name="lucideCrown" aria-hidden="true" />
                        Host
                      </span>
                    }
                  </div>
                  <span class="ready-state" [class.ready-state--ready]="player.isReady">
                    {{ player.isReady ? 'Pronto' : 'Aguardando' }}
                  </span>
                </article>
              }
            </section>

            <section class="lobby-controls" aria-label="Configurações da partida">
              <div class="game-settings">
                <div class="setting-row setting-row--mode">
                  <span>Modo de frases</span>
                  <div
                    class="segmented-control segmented-control--mode"
                    aria-label="Modo de frases"
                  >
                    <button
                      type="button"
                      [class.segmented-control__active]="lobby.mode === 'Classic'"
                      [disabled]="!currentPlayer()?.isHost || actionPending()"
                      (click)="
                        updateSettings(
                          lobby.totalRounds,
                          lobby.phraseSubmissionSeconds,
                          lobby.resultsSeconds,
                          'Classic'
                        )
                      "
                    >
                      Frases dos jogadores
                    </button>
                    <button
                      type="button"
                      [class.segmented-control__active]="lobby.mode === 'AiRandomPhrases'"
                      [disabled]="!currentPlayer()?.isHost || actionPending()"
                      (click)="
                        updateSettings(
                          lobby.totalRounds,
                          lobby.phraseSubmissionSeconds,
                          lobby.resultsSeconds,
                          'AiRandomPhrases'
                        )
                      "
                    >
                      <ng-icon name="lucideSparkles" aria-hidden="true" />
                      Frases aleatórias (IA)
                    </button>
                  </div>
                </div>
                <div class="setting-row">
                  <span>Rodadas</span>
                  <div class="segmented-control" aria-label="Quantidade de rodadas">
                    @for (rounds of [3, 4, 5, 6]; track rounds) {
                      <button
                        type="button"
                        [class.segmented-control__active]="lobby.totalRounds === rounds"
                        [disabled]="!currentPlayer()?.isHost || actionPending()"
                        (click)="
                          updateSettings(
                            rounds,
                            lobby.phraseSubmissionSeconds,
                            lobby.resultsSeconds,
                            lobby.mode
                          )
                        "
                      >
                        {{ rounds }}
                      </button>
                    }
                  </div>
                </div>
                <div class="setting-row">
                  <span>Tempo para frase</span>
                  <div
                    class="segmented-control segmented-control--three"
                    aria-label="Tempo para criar frase"
                  >
                    @for (seconds of [30, 60, 90]; track seconds) {
                      <button
                        type="button"
                        [class.segmented-control__active]="
                          lobby.phraseSubmissionSeconds === seconds
                        "
                        [disabled]="!currentPlayer()?.isHost || actionPending()"
                        (click)="
                          updateSettings(
                            lobby.totalRounds,
                            seconds,
                            lobby.resultsSeconds,
                            lobby.mode
                          )
                        "
                      >
                        {{ seconds }}s
                      </button>
                    }
                  </div>
                </div>
                <div class="setting-row">
                  <span>Tempo de revelação</span>
                  <div
                    class="segmented-control segmented-control--three"
                    aria-label="Tempo de revelação"
                  >
                    @for (seconds of [15, 30, 60]; track seconds) {
                      <button
                        type="button"
                        [class.segmented-control__active]="lobby.resultsSeconds === seconds"
                        [disabled]="!currentPlayer()?.isHost || actionPending()"
                        (click)="
                          updateSettings(
                            lobby.totalRounds,
                            lobby.phraseSubmissionSeconds,
                            seconds,
                            lobby.mode
                          )
                        "
                      >
                        {{ seconds }}s
                      </button>
                    }
                  </div>
                </div>
                @if (!currentPlayer()?.isHost) {
                  <small>Somente o host pode alterar estas opções.</small>
                }
              </div>

              @if (!currentPlayer()?.isHost) {
                <button
                  class="ready-toggle"
                  type="button"
                  role="switch"
                  [attr.aria-checked]="currentPlayer()?.isReady ?? false"
                  [disabled]="actionPending() || realtime.state() !== 'connected'"
                  (click)="toggleReady()"
                >
                  <span class="ready-toggle__track"><i></i></span>
                  Estou pronto
                </button>
              } @else {
                <span class="host-ready">
                  <ng-icon name="lucideCheck" aria-hidden="true" />
                  Host pronto
                </span>
              }
            </section>

            <footer class="lobby-footer">
              <p>
                <ng-icon name="lucideClock" aria-hidden="true" />
                {{
                  lobby.canStart ? 'Todos estão prontos.' : 'Aguardando pelo menos mais um jogador.'
                }}
              </p>
              @if (currentPlayer()?.isHost) {
                <button
                  class="button button--primary button--large"
                  type="button"
                  [disabled]="
                    !lobby.canStart || actionPending() || realtime.state() !== 'connected'
                  "
                  (click)="startGame()"
                >
                  <ng-icon name="lucidePlay" aria-hidden="true" />
                  Iniciar partida
                </button>
              }
            </footer>

            @if (actionMessage()) {
              <p class="lobby-notice" aria-live="polite">{{ actionMessage() }}</p>
            }
          </main>
        } @else if (lobby.status === 'InProgress' || lobby.status === 'Finished') {
          <app-game-phase [gameCode]="lobby.code" />
        } @else {
          <main class="route-state" aria-live="polite">
            <span class="route-state__icon route-state__icon--orange">
              <ng-icon name="lucidePlay" aria-hidden="true" />
            </span>
            <p class="eyebrow eyebrow--dark">RODADA {{ lobby.currentRoundNumber }}</p>
            <h1>Partida iniciada</h1>
            <p>Sincronizando a fase atual da partida.</p>
          </main>
        }
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly gameApi = inject(GameApiService);
  private readonly store = inject(GameStore);

  readonly auth = inject(AuthService);
  readonly realtime = inject(GameRealtimeService);
  readonly requestedCode = this.route.snapshot.paramMap.get('code')?.toUpperCase() ?? '';
  readonly lobby = this.store.lobby;
  readonly status = signal<RoomPageStatus>('loading');
  readonly loadingMessage = signal('Preparando a conexão em tempo real.');
  readonly errorMessage = signal('Não foi possível carregar a sala.');
  readonly actionMessage = signal('');
  readonly actionPending = signal(false);
  readonly copied = signal(false);
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

  ngOnInit(): void {
    void this.initialize();
  }

  ngOnDestroy(): void {
    void this.realtime.stop();
  }

  retry(): void {
    this.status.set('loading');
    this.errorMessage.set('Não foi possível carregar a sala.');
    void this.initialize();
  }

  async toggleReady(): Promise<void> {
    const player = this.currentPlayer();
    const code = this.lobby()?.code;
    if (!player || !code || player.isHost) {
      return;
    }

    await this.runCommand(() => this.realtime.setReady(code, !player.isReady));
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
      window.setTimeout(() => this.copied.set(false), 2_000);
    } catch {
      this.actionMessage.set('Não foi possível copiar o link. Compartilhe o código da sala.');
    }
  }

  private async initialize(): Promise<void> {
    if (this.requestedCode !== 'NOVA' && !/^[A-Z0-9]{5}$/.test(this.requestedCode)) {
      this.showError('O código da sala deve ter exatamente cinco caracteres.');
      return;
    }

    try {
      const user = await firstValueFrom(this.auth.restore());
      if (!user) {
        this.auth.startDiscordLogin(`/sala/${this.requestedCode.toLowerCase()}`);
        return;
      }

      this.loadingMessage.set(
        this.requestedCode === 'NOVA' ? 'Criando uma nova sala.' : 'Entrando na sala.',
      );
      const snapshot = await firstValueFrom(
        this.requestedCode === 'NOVA'
          ? this.gameApi.create({
              totalRounds: 3,
              phraseSubmissionSeconds: 60,
              resultsSeconds: 60,
              mode: 'Classic',
            })
          : this.gameApi.join(this.requestedCode),
      );

      this.store.setSnapshot(snapshot);
      if (this.requestedCode === 'NOVA') {
        this.location.replaceState(`/sala/${snapshot.lobby.code}`);
      }

      await this.realtime.connect(snapshot.lobby.code, {
        stateSynced: (synced) => this.store.setSnapshot(synced),
        lobbyUpdated: (lobby) => this.store.setLobby(lobby),
        presenceChanged: (presence) => this.store.setPresence(presence),
        submissionProgressChanged: (progress) => this.store.setSubmissionProgress(progress),
        commandRejected: (rejection) => this.handleCommandRejected(rejection),
        gameStateChanged: () => void this.syncGame(snapshot.lobby.code),
      });
      this.status.set('ready');
    } catch (error: unknown) {
      this.handleLoadError(error);
    }
  }

  private async runCommand(command: () => Promise<void>): Promise<void> {
    this.actionPending.set(true);
    this.actionMessage.set('');
    try {
      await command();
    } catch {
      this.actionMessage.set('A ação não foi enviada. A conexão será sincronizada novamente.');
      const code = this.lobby()?.code;
      if (code && this.realtime.state() === 'connected') {
        await this.realtime.requestSync(code);
      }
    } finally {
      this.actionPending.set(false);
    }
  }

  private handleCommandRejected(rejection: CommandRejectedMessage): void {
    this.actionMessage.set(commandErrorMessage(rejection.code));
    const code = this.lobby()?.code;
    if (code) {
      void this.realtime.requestSync(code);
    }
  }

  private async syncGame(code: string): Promise<void> {
    if (this.realtime.state() !== 'connected') {
      return;
    }

    try {
      await this.realtime.requestSync(code);
    } catch {
      this.actionMessage.set('O jogo será sincronizado quando a conexão voltar.');
    }
  }

  private handleLoadError(error: unknown): void {
    if (error instanceof ApiProblemError) {
      if (error.status === 401) {
        this.auth.logout();
        this.auth.startDiscordLogin(`/sala/${this.requestedCode.toLowerCase()}`);
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

function commandErrorMessage(code: string): string {
  switch (code) {
    case 'lobby_not_ready':
    case 'not_all_players_ready':
      return 'Todos os jogadores precisam estar prontos.';
    case 'host_required':
      return 'Somente o host pode iniciar a partida.';
    case 'phase_expired':
      return 'Essa etapa terminou. O estado da sala foi atualizado.';
    default:
      return 'A ação foi recusada. O estado da sala foi atualizado.';
  }
}
