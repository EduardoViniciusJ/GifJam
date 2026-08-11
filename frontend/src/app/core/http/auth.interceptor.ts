import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { SessionTokenService } from '@core/auth/session-token.service';
import { apiUrl } from './api-url';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionTokenService);
  const token = session.get();
  const isApiRequest = isGifJamApiRequest(request.url);

  if (!token || !isApiRequest) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  ).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        session.clear();
      }

      return throwError(() => error);
    }),
  );
};

function isGifJamApiRequest(requestUrl: string): boolean {
  const apiBase = new URL(apiUrl('/'), window.location.origin);
  const target = new URL(requestUrl, window.location.origin);
  const apiPath = apiBase.pathname.endsWith('/') ? apiBase.pathname.slice(0, -1) : apiBase.pathname;

  return (
    target.origin === apiBase.origin &&
    (target.pathname === apiPath || target.pathname.startsWith(`${apiPath}/`))
  );
}
