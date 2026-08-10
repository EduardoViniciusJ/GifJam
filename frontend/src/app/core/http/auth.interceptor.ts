import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { SessionTokenService } from '@core/auth/session-token.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(SessionTokenService).get();
  const isApiRequest = request.url.startsWith('/api') || request.url.includes('/api/');

  if (!token || !isApiRequest) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
