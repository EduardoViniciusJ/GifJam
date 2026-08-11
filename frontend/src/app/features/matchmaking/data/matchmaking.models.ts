export type MatchmakingStatus = 'NotInQueue' | 'Waiting' | 'Matched';

export interface MatchmakingSnapshot {
  status: MatchmakingStatus;
  playerCount: number;
  minimumPlayers: number;
  maximumPlayers: number;
  hostUserId: string | null;
  deadlineAt: string | null;
  gameCode: string | null;
  gameMode: 'Classic' | 'AiRandomPhrases' | null;
  serverTime: string;
}

export interface MatchFoundSnapshot {
  gameCode: string;
  hostUserId: string;
  playerCount: number;
  serverTime: string;
}
