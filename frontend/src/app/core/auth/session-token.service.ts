import { Injectable } from '@angular/core';

const SESSION_TOKEN_KEY = 'gifjam.session.token';

@Injectable({ providedIn: 'root' })
export class SessionTokenService {
  get(): string | null {
    return sessionStorage.getItem(SESSION_TOKEN_KEY);
  }

  set(token: string): void {
    sessionStorage.setItem(SESSION_TOKEN_KEY, token);
  }

  clear(): void {
    sessionStorage.removeItem(SESSION_TOKEN_KEY);
  }
}
