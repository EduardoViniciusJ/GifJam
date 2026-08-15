import { GameMode } from '@features/game/data/game.models';

export type RoomDirectorySort = 'popular' | 'recent';

export interface PublicRoomSummary {
  code: string;
  mode: GameMode;
  totalRounds: number;
  hostDisplayName: string;
  hostAvatarUrl: string | null;
  playerCount: number;
  capacity: number;
  createdAt: string;
}

export interface PublicRoomDirectoryResponse {
  items: PublicRoomSummary[];
  page: number;
  pageSize: number;
  total: number;
  serverTime: string;
}
