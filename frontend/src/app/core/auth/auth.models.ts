export interface SessionUser {
  id: string;
  discordId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  totalScore?: number;
  rank?: number | null;
}

export interface AuthExchangeResponse {
  expiresAt: string;
  user: SessionUser;
  csrfToken: string;
}

export interface AuthStatusResponse {
  user: SessionUser;
  csrfToken: string;
}
