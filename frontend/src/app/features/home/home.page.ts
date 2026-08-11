import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  inject,
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

  joinRoom(): void {
    this.roomCode.markAsTouched();

    if (this.roomCode.invalid) {
      return;
    }

    void this.router.navigate(['/sala', this.roomCode.value]);
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
}
