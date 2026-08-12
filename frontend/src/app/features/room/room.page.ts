import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowLeft,
  lucideCheck,
  lucideCircleAlert,
  lucideClock,
  lucideCopy,
  lucideCrown,
  lucideGamepad2,
  lucideLink,
  lucideLoaderCircle,
  lucideLogOut,
  lucidePlay,
  lucideRefreshCw,
  lucideSparkles,
  lucideSettings2,
  lucideTrophy,
  lucideUserPlus,
  lucideUsers,
  lucideWifi,
  lucideWifiOff,
  lucideX,
} from '@ng-icons/lucide';
import { GameRealtimeService } from '@features/game/data/game-realtime.service';
import { GameMode, LobbyPlayerSnapshot } from '@features/game/data/game.models';
import { GameStore } from '@features/game/state/game.store';
import { BrandComponent } from '@shared/ui/brand/brand.component';
import { GamePhasePage } from '@features/game/game-phase.page';
import { RoomFacade } from './state/room.facade';

@Component({
  selector: 'app-room-page',
  imports: [BrandComponent, GamePhasePage, NgIcon, RouterLink],
  providers: [
    GameStore,
    GameRealtimeService,
    RoomFacade,
    provideIcons({
      lucideArrowLeft,
      lucideCheck,
      lucideCircleAlert,
      lucideClock,
      lucideCopy,
      lucideCrown,
      lucideGamepad2,
      lucideLink,
      lucideLoaderCircle,
      lucideLogOut,
      lucidePlay,
      lucideRefreshCw,
      lucideSparkles,
      lucideSettings2,
      lucideTrophy,
      lucideUserPlus,
      lucideUsers,
      lucideWifi,
      lucideWifiOff,
      lucideX,
    }),
  ],
  templateUrl: './room.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly facade = inject(RoomFacade);

  readonly auth = this.facade.auth;
  readonly realtime = this.facade.realtime;
  readonly requestedCode = this.route.snapshot.paramMap.get('code')?.toUpperCase() ?? '';
  readonly lobby = this.facade.lobby;
  readonly status = this.facade.status;
  readonly loadingMessage = this.facade.loadingMessage;
  readonly errorMessage = this.facade.errorMessage;
  readonly actionMessage = this.facade.actionMessage;
  readonly actionPending = this.facade.actionPending;
  readonly leavingRoom = this.facade.leavingRoom;
  readonly copied = this.facade.copied;
  readonly globalRanking = this.facade.globalRanking;
  readonly globalRankingStatus = this.facade.globalRankingStatus;
  readonly currentPlayer = this.facade.currentPlayer;
  readonly connectionLabel = this.facade.connectionLabel;
  readonly hostPlayer = computed(
    () => this.lobby()?.players.find((player) => player.isHost) ?? null,
  );
  readonly currentGlobalRanking = computed(
    () =>
      this.globalRanking()?.entries.find((entry) => entry.userId === this.auth.user()?.id) ?? null,
  );
  readonly playersWithRanking = computed<LobbyPlayerWithRanking[]>(() => {
    const currentUserId = this.auth.user()?.id;
    const rankingIsReady = this.globalRankingStatus() === 'ready';
    const rankingByUserId = new Map(
      (this.globalRanking()?.entries ?? []).map((entry) => [entry.userId, entry]),
    );

    return (this.lobby()?.players ?? []).map((player) => {
      const ranking = rankingByUserId.get(player.userId) ?? null;

      return {
        player,
        position: ranking?.position ?? null,
        totalScore: rankingIsReady ? (ranking?.score ?? 0) : null,
        isCurrent: player.userId === currentUserId,
      };
    });
  });
  readonly currentPlayerPosition = computed(() => this.currentGlobalRanking()?.position ?? null);
  readonly currentPlayerScore = computed<number | null>(() =>
    this.globalRankingStatus() === 'ready' ? (this.currentGlobalRanking()?.score ?? 0) : null,
  );
  readonly openPlayerSlots = computed(() =>
    Array.from(
      { length: Math.min(2, Math.max(0, 6 - this.playersWithRanking().length)) },
      (_, index) => index,
    ),
  );

  ngOnInit(): void {
    void this.facade.initialize(this.requestedCode);
  }

  ngOnDestroy(): void {
    void this.facade.destroy();
  }

  retry(): void {
    void this.facade.retry(this.requestedCode);
  }

  async toggleReady(): Promise<void> {
    await this.facade.toggleReady();
  }

  async updateSettings(
    totalRounds: number,
    phraseSubmissionSeconds: number,
    resultsSeconds: number,
    mode: GameMode,
  ): Promise<void> {
    await this.facade.updateSettings(totalRounds, phraseSubmissionSeconds, resultsSeconds, mode);
  }

  async startGame(): Promise<void> {
    await this.facade.startGame();
  }

  async leaveRoom(): Promise<void> {
    await this.facade.leaveRoom();
  }

  async copyInvite(): Promise<void> {
    await this.facade.copyInvite();
  }

  dismissActionMessage(): void {
    this.facade.dismissActionMessage();
  }
}

interface LobbyPlayerWithRanking {
  player: LobbyPlayerSnapshot;
  position: number | null;
  totalScore: number | null;
  isCurrent: boolean;
}
