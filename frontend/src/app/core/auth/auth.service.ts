import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';

import { ApiProblemError } from '@core/models/problem-details.model';
import { apiUrl } from '@core/http/api-url';

import { AuthExchangeResponse, AuthStatusResponse, SessionUser } from './auth.models';
import { SessionTokenService } from './session-token.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionTokenService);
  private restoreRequest: Observable<SessionUser | null> | null = null;

  readonly user = this.session.user;
  readonly isAuthenticated = this.session.isAuthenticated;

  exchange(code: string): Observable<AuthExchangeResponse> {
    return this.http
      .post<AuthExchangeResponse>(apiUrl('/auth/exchange'), { code })
      .pipe(tap((response) => this.session.setSession(response.user, response.csrfToken)));
  }

  restore(): Observable<SessionUser | null> {
    if (this.restoreRequest) {
      return this.restoreRequest;
    }

    const request = this.http.get<AuthStatusResponse>(apiUrl('/auth/me')).pipe(
      tap((response) => this.session.setSession(response.user, response.csrfToken)),
      map((response) => response.user),
      catchError((error: unknown) => {
        if (error instanceof ApiProblemError && error.status === 401) {
          this.session.clear();
          return of(null);
        }

        return throwError(() => error);
      }),
      finalize(() => {
        if (this.restoreRequest === request) {
          this.restoreRequest = null;
        }
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.restoreRequest = request;
    return request;
  }

  startDiscordLogin(returnUrl: string): void {
    const query = new URLSearchParams({ returnUrl: normalizeReturnUrl(returnUrl) });
    window.location.assign(`${apiUrl('/auth/discord/start')}?${query.toString()}`);
  }

  logout(): void {
    this.http.post<void>(apiUrl('/auth/logout'), {}).subscribe({ error: () => undefined });
    this.session.clear();
  }

  deleteAccount(confirmation: string): Observable<void> {
    return this.http
      .delete<void>(apiUrl('/auth/account'), { body: { confirmation } })
      .pipe(tap(() => this.session.clear()));
  }
}

function normalizeReturnUrl(value: string): string {
  return value.startsWith('/') && !value.startsWith('//') ? value : '/';
}
