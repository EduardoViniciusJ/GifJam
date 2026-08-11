import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
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
import { GameRealtimeService } from '@features/game/data/game-realtime.service';
import { GameMode } from '@features/game/data/game.models';
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
  readonly copied = this.facade.copied;
  readonly currentPlayer = this.facade.currentPlayer;
  readonly connectionLabel = this.facade.connectionLabel;

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

  async copyInvite(): Promise<void> {
    await this.facade.copyInvite();
  }
}
