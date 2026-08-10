export interface SessionUser {
  id: string;
  discordId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
}

export interface AuthExchangeResponse {
  accessToken: string;
  expiresAt: string;
  user: SessionUser;
}
