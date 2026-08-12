import { Injectable, computed, signal } from '@angular/core';

import { SessionUser } from './auth.models';

@Injectable({ providedIn: 'root' })
export class SessionTokenService {
  private readonly userState = signal<SessionUser | null>(null);
  private csrfToken: string | null = null;

  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => Boolean(this.userState()));

  get(): string | null {
    return null;
  }

  set(_token: string, user?: SessionUser): void {
    if (user) {
      this.setUser(user);
    }
  }

  setSession(user: SessionUser, csrfToken: string): void {
    this.setUser(user);
    this.csrfToken = csrfToken.trim() || null;
  }

  getCsrfToken(): string | null {
    return this.csrfToken;
  }

  setUser(user: SessionUser): void {
    if (!isSessionUser(user)) {
      this.userState.set(null);
      return;
    }

    this.userState.set(user);
  }

  clear(): void {
    this.userState.set(null);
    this.csrfToken = null;
  }
}

function isSessionUser(value: unknown): value is SessionUser {
  if (!value || typeof value !== 'object') return false;
  const candidate = value as Record<string, unknown>;
  return (
    ['id', 'discordId', 'username', 'displayName'].every(
      (key) => typeof candidate[key] === 'string' && (candidate[key] as string).trim().length > 0,
    ) &&
    (candidate['avatarUrl'] === null || typeof candidate['avatarUrl'] === 'string')
  );
}
