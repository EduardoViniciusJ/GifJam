export type GameStatus = 'Lobby' | 'InProgress' | 'Finished' | 'Closed';
export type GameMode = 'Classic' | 'AiRandomPhrases';
export type RoundPhase =
  'PhraseSubmission' | 'PhraseVoting' | 'GifSubmission' | 'GifVoting' | 'Results' | 'Completed';

export interface LobbyPlayerSnapshot {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  score: number;
  isReady: boolean;
  isConnected: boolean;
  isHost: boolean;
}

export interface LobbySnapshot {
  code: string;
  status: GameStatus;
  mode: GameMode;
  totalRounds: number;
  phraseSubmissionSeconds: number;
  resultsSeconds: number;
  currentRoundNumber: number;
  hostUserId: string;
  canStart: boolean;
  players: LobbyPlayerSnapshot[];
  serverTime: string;
}

export interface PlayerGameSnapshot {
  lobby: LobbySnapshot;
  isHost: boolean;
  round: PlayerRoundSnapshot | null;
}

export interface PresenceSnapshot {
  code: string;
  players: { userId: string; isConnected: boolean }[];
  serverTime: string;
}

export interface SubmissionProgressSnapshot {
  completed: number;
  eligible: number;
  serverTime: string;
}

export interface CommandRejectedMessage {
  code: string;
  message: string;
  currentPhase?: string | null;
}

export interface AnonymousPhraseSnapshot {
  id: string;
  text: string;
}

export interface PlayerPhraseSnapshot extends AnonymousPhraseSnapshot {
  isOwn: boolean;
}

export interface SelectedPhraseSnapshot {
  id: string;
  text: string;
}

export interface PlayerGifSelectionSnapshot {
  externalId: string;
  description: string;
  previewUrl: string;
  mediaUrl: string;
  width: number;
  height: number;
  previewWidth: number;
  previewHeight: number;
  sourceUrl: string;
  attribution: string;
}

export interface PlayerGifSnapshot {
  id: string;
  description: string;
  previewUrl: string;
  mediaUrl: string;
  width: number;
  height: number;
  previewWidth: number;
  previewHeight: number;
  sourceUrl: string;
  attribution: string;
  isOwn: boolean;
}

export interface RevealedPlayerSnapshot {
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
}

export interface RevealedPhraseSnapshot {
  id: string;
  text: string;
  source: 'Player' | 'Ai';
  author: RevealedPlayerSnapshot | null;
}

export interface RevealedGifSnapshot {
  id: string;
  description: string;
  previewUrl: string;
  mediaUrl: string;
  width: number;
  height: number;
  previewWidth: number;
  previewHeight: number;
  sourceUrl: string;
  attribution: string;
  author: RevealedPlayerSnapshot;
  voteCount: number;
  position: number;
}

export interface RoundRevealSnapshot {
  roundNumber: number;
  phrase: RevealedPhraseSnapshot | null;
  gifs: RevealedGifSnapshot[];
  serverTime: string;
}

export interface RankingEntrySnapshot {
  position: number;
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  score: number;
}

export interface RankingSnapshot {
  gameCode: string;
  completedRounds: number;
  isFinal: boolean;
  entries: RankingEntrySnapshot[];
  serverTime: string;
}

export interface GlobalRankingEntry {
  position: number;
  userId: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  score: number;
  isCurrentUser: boolean;
}

export interface GlobalRankingSnapshot {
  entries: GlobalRankingEntry[];
  serverTime: string;
}

export interface PlayerRoundSnapshot {
  roundNumber: number;
  phase: RoundPhase;
  phaseEndsAt: string;
  gifVotingPresentationEndsAt: string | null;
  hasSubmittedPhrase: boolean;
  hasVotedPhrase: boolean;
  hasSubmittedGif: boolean;
  hasVotedGif: boolean;
  hasConfirmedResults: boolean;
  phrases: PlayerPhraseSnapshot[];
  gifs: PlayerGifSnapshot[];
  selectedPhrase: SelectedPhraseSnapshot | null;
  gifSelection: PlayerGifSelectionSnapshot | null;
  reveal: RoundRevealSnapshot | null;
  ranking: RankingSnapshot | null;
  serverTime: string;
}

export interface GameSettings {
  mode: GameMode;
  totalRounds: number;
  phraseSubmissionSeconds: number;
  resultsSeconds: number;
}

export interface GifSearchItem {
  id: string;
  description: string;
  previewUrl: string;
  mediaUrl: string;
  width: number;
  height: number;
  previewWidth: number;
  previewHeight: number;
  sourceUrl: string;
  attribution: string;
  selectionToken: string;
}

export interface GifSearchResponse {
  items: GifSearchItem[];
  nextCursor: string | null;
  searchPlaceholder: string;
  attribution: string;
}
