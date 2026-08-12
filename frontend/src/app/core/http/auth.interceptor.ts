import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { SessionTokenService } from '@core/auth/session-token.service';
import { apiUrl } from './api-url';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionTokenService);
  const isApiRequest = isGifJamApiRequest(request.url);

  if (!isApiRequest) {
    return next(request);
  }

  const headers: Record<string, string> = {};
  const csrfToken = readCookie('gifjam-csrf');
  if (csrfToken && isUnsafeMethod(request.method)) {
    headers['X-CSRF-TOKEN'] = csrfToken;
  }

  return next(
    request.clone({
      withCredentials: true,
      setHeaders: headers,
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

function isUnsafeMethod(method: string): boolean {
  return ['POST', 'PUT', 'PATCH', 'DELETE'].includes(method.toUpperCase());
}

function readCookie(name: string): string | null {
  if (typeof document === 'undefined') {
    return null;
  }

  const prefix = `${name}=`;
  return (
    document.cookie
      .split('; ')
      .find((cookie) => cookie.startsWith(prefix))
      ?.slice(prefix.length) ?? null
  );
}

function isGifJamApiRequest(requestUrl: string): boolean {
  const apiBase = new URL(apiUrl('/'), window.location.origin);
  const target = new URL(requestUrl, window.location.origin);
  const apiPath = apiBase.pathname.endsWith('/') ? apiBase.pathname.slice(0, -1) : apiBase.pathname;

  return (
    target.origin === apiBase.origin &&
    (target.pathname === apiPath || target.pathname.startsWith(`${apiPath}/`))
  );
}
