import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { catchError, map, of } from 'rxjs';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated()) {
    return true;
  }

  return auth.restore().pipe(
    map((user) => (user ? true : router.createUrlTree(['/']))),
    catchError(() => of(router.createUrlTree(['/']))),
  );
};
