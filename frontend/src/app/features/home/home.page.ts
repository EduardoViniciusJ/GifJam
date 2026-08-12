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
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowRight,
  lucideCrown,
  lucideLogIn,
  lucideLogOut,
  lucideLoaderCircle,
  lucideMessageSquare,
  lucideSmile,
  lucideTrophy,
  lucideUsers,
  lucideVote,
} from '@ng-icons/lucide';

import { BrandComponent } from '@shared/ui/brand/brand.component';
import { AuthService } from '@core/auth/auth.service';
import { MatchmakingRealtimeService } from '@features/matchmaking/data/matchmaking-realtime.service';
import { MatchmakingFacade } from '@features/matchmaking/state/matchmaking.facade';

@Component({
  selector: 'app-home-page',
  imports: [BrandComponent, NgIcon, ReactiveFormsModule, RouterLink],
  providers: [
    MatchmakingRealtimeService,
    MatchmakingFacade,
    provideIcons({
      lucideArrowRight,
      lucideCrown,
      lucideLogIn,
      lucideLogOut,
      lucideLoaderCircle,
      lucideMessageSquare,
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

  readonly user = this.auth.user;
  readonly isAuthenticated = this.auth.isAuthenticated;
  readonly joiningRoom = signal(false);

  readonly roomCode = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[A-Z0-9]{5}$/)],
  });

  ngOnInit(): void {
    this.auth
      .restore()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => void this.matchmaking.initialize(),
        error: () => void this.matchmaking.initialize(),
      });
  }

  ngOnDestroy(): void {
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
    if (!this.isAuthenticated()) {
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

  createRoom(): void {
    if (this.isAuthenticated()) {
      void this.router.navigate(['/sala', 'nova']);
      return;
    }

    this.auth.startDiscordLogin('/sala/nova');
  }

  login(): void {
    if (!this.isAuthenticated()) {
      this.auth.startDiscordLogin('/');
    }
  }

  enterMatchmaking(): void {
    if (!this.isAuthenticated()) {
      this.auth.startDiscordLogin('/');
      return;
    }

    void this.matchmaking.toggleQueue();
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
}
