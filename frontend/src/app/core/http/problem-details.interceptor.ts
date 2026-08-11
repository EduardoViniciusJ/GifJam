import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

import { ApiProblemError, ProblemDetails } from '@core/models/problem-details.model';

export const problemDetailsInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const problem = isProblemDetails(error.error)
        ? error.error
        : {
            title: 'Falha de comunicação',
            detail: fallbackMessage(error.status),
            status: error.status,
          };

      return throwError(() => new ApiProblemError(problem, error.status));
    }),
  );

function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const problem = value as ProblemDetails;
  return typeof problem.title === 'string' && typeof problem.detail === 'string';
}

function fallbackMessage(status: number): string {
  if (status === 0) {
    return 'Não foi possível conectar ao GifJam. Verifique sua conexão e tente novamente.';
  }

  return 'O servidor não conseguiu concluir a solicitação.';
}
