import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideArrowRight,
  lucideCrown,
  lucideLogIn,
  lucideMessageSquare,
  lucideShieldCheck,
  lucideSmile,
  lucideTrophy,
  lucideUsers,
  lucideVote,
} from '@ng-icons/lucide';

import { BrandComponent } from '@shared/ui/brand/brand.component';

@Component({
  selector: 'app-home-page',
  imports: [BrandComponent, NgIcon, ReactiveFormsModule, RouterLink],
  providers: [
    provideIcons({
      lucideArrowRight,
      lucideCrown,
      lucideLogIn,
      lucideMessageSquare,
      lucideShieldCheck,
      lucideSmile,
      lucideTrophy,
      lucideUsers,
      lucideVote,
    }),
  ],
  templateUrl: './home.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomePage {
  private readonly router = inject(Router);

  readonly roomCode = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[A-Z0-9]{5}$/)],
  });

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
    this.startDiscordLogin('/sala/nova');
  }

  login(): void {
    this.startDiscordLogin('/');
  }

  private startDiscordLogin(returnUrl: string): void {
    const query = new URLSearchParams({ returnUrl });
    window.location.assign(`/api/auth/discord/start?${query.toString()}`);
  }
}
