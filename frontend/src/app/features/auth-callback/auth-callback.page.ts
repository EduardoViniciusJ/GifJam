import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCircleAlert, lucideLoaderCircle, lucideRefreshCw } from '@ng-icons/lucide';
import { TimeoutError, timeout } from 'rxjs';

import { AuthService } from '@core/auth/auth.service';
import { ApiProblemError } from '@core/models/problem-details.model';
import { BrandComponent } from '@shared/ui/brand/brand.component';

type CallbackStatus = 'loading' | 'error';

@Component({
  selector: 'app-auth-callback-page',
  imports: [BrandComponent, NgIcon],
  providers: [provideIcons({ lucideCircleAlert, lucideLoaderCircle, lucideRefreshCw })],
  template: `
    <main class="status-page" aria-live="polite">
      <app-brand />
      @if (status() === 'loading') {
        <ng-icon class="status-page__spinner" name="lucideLoaderCircle" aria-hidden="true" />
        <h1>Conectando sua conta</h1>
        <p>Aguarde enquanto concluímos a entrada com o Discord.</p>
      } @else {
        <span class="route-state__icon route-state__icon--orange">
          <ng-icon name="lucideCircleAlert" aria-hidden="true" />
        </span>
        <h1>Não foi possível entrar</h1>
        <p>{{ errorMessage() }}</p>
        <button class="button button--primary" type="button" (click)="retry()">
          <ng-icon name="lucideRefreshCw" aria-hidden="true" />
          <span>Tentar novamente</span>
        </button>
      }
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthCallbackPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly returnUrl = normalizeReturnUrl(
    this.route.snapshot.queryParamMap.get('returnUrl'),
  );

  readonly status = signal<CallbackStatus>('loading');
  readonly errorMessage = signal('Não foi possível concluir a entrada com o Discord.');

  ngOnInit(): void {
    const providerError = this.route.snapshot.queryParamMap.get('error');
    const code = this.route.snapshot.queryParamMap.get('code');

    if (providerError) {
      this.showError(callbackErrorMessage(providerError));
      return;
    }

    if (!code) {
      this.showError('O link de entrada está incompleto. Inicie a conexão novamente.');
      return;
    }

    this.auth
      .exchange(code)
      .pipe(timeout(10_000), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => void this.router.navigateByUrl(this.returnUrl, { replaceUrl: true }),
        error: (error: unknown) => this.handleExchangeError(error),
      });
  }

  retry(): void {
    this.auth.startDiscordLogin(this.returnUrl);
  }

  private handleExchangeError(error: unknown): void {
    if (error instanceof TimeoutError) {
      this.showError('O servidor demorou para responder. Verifique a conexão e tente novamente.');
      return;
    }

    if (error instanceof ApiProblemError && error.problem.code === 'invalid_exchange_code') {
      this.showError('Este link de entrada expirou ou já foi utilizado. Conecte-se novamente.');
      return;
    }

    if (error instanceof ApiProblemError && error.problem.code === 'discord_exchange_failed') {
      this.showError('A autorização do Discord expirou. Conecte-se novamente.');
      return;
    }

    this.showError('Não foi possível concluir a entrada. Tente novamente em instantes.');
  }

  private showError(message: string): void {
    this.errorMessage.set(message);
    this.status.set('error');
  }
}

function normalizeReturnUrl(value: string | null): string {
  return value?.startsWith('/') && !value.startsWith('//') ? value : '/';
}

function callbackErrorMessage(error: string): string {
  if (error === 'access_denied') {
    return 'A entrada com o Discord foi cancelada ou não foi autorizada.';
  }

  if (error === 'discord_exchange_failed') {
    return 'A autorização do Discord expirou ou já foi utilizada. Conecte-se novamente.';
  }

  return 'O Discord não concluiu a autenticação. Tente novamente em instantes.';
}
