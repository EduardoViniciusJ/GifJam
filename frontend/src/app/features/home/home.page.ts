import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowRight,
  lucideCrown,
  lucideDoorOpen,
  lucideLogIn,
  lucideLogOut,
  lucideLoaderCircle,
  lucideMessageSquare,
  lucideRefreshCw,
  lucideSmile,
  lucideTrophy,
  lucideUsers,
  lucideVote,
} from '@ng-icons/lucide';

import { BrandComponent } from '@shared/ui/brand/brand.component';
import { AuthService } from '@core/auth/auth.service';
import { MatchmakingRealtimeService } from '@features/matchmaking/data/matchmaking-realtime.service';
import { MatchmakingFacade } from '@features/matchmaking/state/matchmaking.facade';
import { PublicRoomSummary } from '@features/rooms/data/room-directory.models';
import { RoomDirectoryRealtimeService } from '@features/rooms/data/room-directory-realtime.service';
import { RoomDirectoryFacade } from '@features/rooms/state/room-directory.facade';
import { RoomCardComponent } from '@features/rooms/ui/room-card/room-card.component';

@Component({
  selector: 'app-home-page',
  imports: [BrandComponent, NgIcon, ReactiveFormsModule, RoomCardComponent, RouterLink],
  providers: [
    MatchmakingRealtimeService,
    MatchmakingFacade,
    RoomDirectoryRealtimeService,
    RoomDirectoryFacade,
    provideIcons({
      lucideArrowRight,
      lucideCrown,
      lucideDoorOpen,
      lucideLogIn,
      lucideLogOut,
      lucideLoaderCircle,
      lucideMessageSquare,
      lucideRefreshCw,
      lucideSmile,
      lucideTrophy,
      lucideUsers,
      lucideVote,
    }),
  ],
  templateUrl: './home.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  readonly matchmaking = inject(MatchmakingFacade);
  readonly rooms = inject(RoomDirectoryFacade);

  readonly user = this.auth.user;
  readonly isAuthenticated = this.auth.isAuthenticated;
  readonly joiningRoom = signal(false);

  readonly roomCode = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[A-Z0-9]{5}$/)],
  });

  ngOnInit(): void {
    void this.rooms.initialize({ pageSize: 5, sort: 'popular' });
    this.auth
      .restore()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => void this.matchmaking.initialize(),
        error: () => void this.matchmaking.initialize(),
      });
  }

  ngOnDestroy(): void {
    void this.rooms.destroy();
    void this.matchmaking.destroy();
  }

  normalizeCode(event: Event): void {
    const input = event.target as HTMLInputElement;
    const normalized = input.value
      .toUpperCase()
      .replace(/[^A-Z0-9]/g, '')
      .slice(0, 5);
    this.roomCode.setValue(normalized);
  }

  async joinRoom(): Promise<void> {
    this.roomCode.markAsTouched();

    if (this.roomCode.invalid || this.joiningRoom()) {
      return;
    }

    const code = this.roomCode.value;
    this.joiningRoom.set(true);
    if (!(await this.hasAuthenticatedSession())) {
      this.auth.startDiscordLogin(`/sala/${code.toLowerCase()}`);
      return;
    }

    try {
      const navigated = await this.router.navigate(['/sala', code]);
      if (!navigated) {
        this.joiningRoom.set(false);
      }
    } catch {
      this.joiningRoom.set(false);
    }
  }

  async createRoom(): Promise<void> {
    if (await this.hasAuthenticatedSession()) {
      void this.router.navigate(['/sala', 'nova']);
      return;
    }

    this.auth.startDiscordLogin('/sala/nova');
  }

  async login(): Promise<void> {
    if (!(await this.hasAuthenticatedSession())) {
      this.auth.startDiscordLogin('/');
    }
  }

  async enterMatchmaking(): Promise<void> {
    if (!(await this.hasAuthenticatedSession())) {
      this.auth.startDiscordLogin('/');
      return;
    }

    void this.matchmaking.toggleQueue();
  }

  openPublicRoom(room: PublicRoomSummary): void {
    void this.rooms.openRoom(room);
  }

  async logout(): Promise<void> {
    if (this.matchmaking.isWaiting()) {
      await this.matchmaking.leaveQueue();
    }

    await this.matchmaking.destroy();
    this.auth.logout();
  }

  openProfile(): void {
    void this.router.navigate(['/perfil']);
  }

  private async hasAuthenticatedSession(): Promise<boolean> {
    if (this.isAuthenticated()) {
      return true;
    }

    try {
      return Boolean(await firstValueFrom(this.auth.restore()));
    } catch {
      return false;
    }
  }
}
