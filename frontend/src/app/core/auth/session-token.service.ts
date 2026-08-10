import { Injectable, computed, signal } from '@angular/core';

import { SessionUser } from './auth.models';

const SESSION_TOKEN_KEY = 'gifjam.session.token';
const SESSION_USER_KEY = 'gifjam.session.user';

@Injectable({ providedIn: 'root' })
export class SessionTokenService {
  private readonly tokenState = signal(readSessionToken());
  private readonly userState = signal(readSessionUser());

  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => Boolean(this.tokenState()));

  get(): string | null {
    return this.tokenState();
  }

  set(token: string, user?: SessionUser): void {
    const normalizedToken = token.trim();
    if (!normalizedToken) {
      this.clear();
      return;
    }

    writeSessionValue(SESSION_TOKEN_KEY, normalizedToken);
    this.tokenState.set(normalizedToken);

    if (user) {
      this.setUser(user);
    }
  }

  setUser(user: SessionUser): void {
    if (!isSessionUser(user)) {
      removeSessionValue(SESSION_USER_KEY);
      this.userState.set(null);
      return;
    }

    writeSessionValue(SESSION_USER_KEY, JSON.stringify(user));
    this.userState.set(user);
  }

  clear(): void {
    removeSessionValue(SESSION_TOKEN_KEY);
    removeSessionValue(SESSION_USER_KEY);
    this.tokenState.set(null);
    this.userState.set(null);
  }
}

function readSessionToken(): string | null {
  const token = readSessionValue(SESSION_TOKEN_KEY);
  return token?.trim() || null;
}

function readSessionUser(): SessionUser | null {
  const stored = readSessionValue(SESSION_USER_KEY);
  if (!stored) {
    return null;
  }

  try {
    const parsed: unknown = JSON.parse(stored);
    if (isSessionUser(parsed)) {
      return parsed;
    }
  } catch {
    // Invalid persisted data is discarded below.
  }

  removeSessionValue(SESSION_USER_KEY);
  return null;
}

function isSessionUser(value: unknown): value is SessionUser {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Record<string, unknown>;
  return (
    isNonEmptyString(candidate['id']) &&
    isNonEmptyString(candidate['discordId']) &&
    isNonEmptyString(candidate['username']) &&
    isNonEmptyString(candidate['displayName']) &&
    (candidate['avatarUrl'] === null || typeof candidate['avatarUrl'] === 'string')
  );
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

function readSessionValue(key: string): string | null {
  try {
    return typeof window === 'undefined' ? null : window.sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeSessionValue(key: string, value: string): void {
  try {
    if (typeof window !== 'undefined') {
      window.sessionStorage.setItem(key, value);
    }
  } catch {
    // The in-memory signal remains usable when browser storage is unavailable.
  }
}

function removeSessionValue(key: string): void {
  try {
    if (typeof window !== 'undefined') {
      window.sessionStorage.removeItem(key);
    }
  } catch {
    // Storage cleanup is best effort; the in-memory state is cleared by the caller.
  }
}
