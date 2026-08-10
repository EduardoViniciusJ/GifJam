import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of, tap, throwError } from 'rxjs';

import { ApiProblemError } from '@core/models/problem-details.model';

import { AuthExchangeResponse, SessionUser } from './auth.models';
import { SessionTokenService } from './session-token.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionTokenService);

  readonly user = this.session.user;
  readonly isAuthenticated = this.session.isAuthenticated;

  exchange(code: string): Observable<AuthExchangeResponse> {
    return this.http
      .post<AuthExchangeResponse>('/api/auth/exchange', { code })
      .pipe(tap((response) => this.session.set(response.accessToken, response.user)));
  }

  restore(): Observable<SessionUser | null> {
    if (!this.session.get()) {
      return of(null);
    }

    return this.http.get<SessionUser>('/api/auth/me').pipe(
      tap((user) => this.session.setUser(user)),
      catchError((error: unknown) => {
        if (error instanceof ApiProblemError && error.status === 401) {
          this.session.clear();
          return of(null);
        }

        return throwError(() => error);
      }),
    );
  }

  startDiscordLogin(returnUrl: string): void {
    const query = new URLSearchParams({ returnUrl: normalizeReturnUrl(returnUrl) });
    window.location.assign(`/api/auth/discord/start?${query.toString()}`);
  }

  logout(): void {
    this.session.clear();
  }
}

function normalizeReturnUrl(value: string): string {
  return value.startsWith('/') && !value.startsWith('//') ? value : '/';
}
