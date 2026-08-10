export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export class ApiProblemError extends Error {
  constructor(
    readonly problem: ProblemDetails,
    readonly status: number,
  ) {
    super(problem.detail ?? problem.title ?? 'Não foi possível concluir a solicitação.');
    this.name = 'ApiProblemError';
  }
}
