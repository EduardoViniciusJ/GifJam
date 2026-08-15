import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from './auth.service';

export const roomAuthGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) {
    return true;
  }

  const startLogin = (): false => {
    auth.startDiscordLogin(state.url);
    return false;
  };

  return auth.restore().pipe(
    map((user) => (user ? true : startLogin())),
    catchError(() => of(startLogin())),
  );
};
