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
    super(userFacingMessage(problem.code, status));
    this.name = 'ApiProblemError';
  }
}

function userFacingMessage(code: string | undefined, status: number): string {
  switch (code) {
    case 'game_not_found':
      return 'A sala não existe ou já foi encerrada.';
    case 'game_full':
      return 'A sala já está cheia.';
    case 'game_already_started':
      return 'A partida já começou.';
    case 'gif_provider_unavailable':
      return 'A busca de GIF está indisponível no momento.';
    case 'invalid_exchange_code':
      return 'Este link de entrada expirou ou já foi utilizado.';
  }

  if (status === 401) {
    return 'Sua sessão expirou. Entre novamente para continuar.';
  }

  if (status === 403) {
    return 'Você não tem acesso a esta ação.';
  }

  if (status === 404) {
    return 'O conteúdo solicitado não está disponível.';
  }

  if (status === 429) {
    return 'Muitas tentativas em pouco tempo. Aguarde e tente novamente.';
  }

  if (status >= 500) {
    return 'O serviço está temporariamente indisponível. Tente novamente em instantes.';
  }

  return 'Não foi possível concluir a solicitação.';
}
