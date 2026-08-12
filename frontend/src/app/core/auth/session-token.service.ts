import { Injectable, computed, signal } from '@angular/core';

import { SessionUser } from './auth.models';

@Injectable({ providedIn: 'root' })
export class SessionTokenService {
  private readonly userState = signal<SessionUser | null>(null);

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

  setUser(user: SessionUser): void {
    if (!isSessionUser(user)) {
      this.userState.set(null);
      return;
    }

    this.userState.set(user);
  }

  clear(): void {
    this.userState.set(null);
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
