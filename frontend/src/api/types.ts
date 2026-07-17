// Request/response shapes for the Solitaire API. These mirror the backend
// contracts (Solitaire.Api/Auth/Contracts.cs and Leaderboard/Contracts.cs);
// keep them in sync when the server contracts change.

import type { MoveDto } from '../engine/types';

/** The signed-in user as returned by /register, /login and /me. */
export interface UserResponse {
  readonly id: string;
  /** Public handle shown on the leaderboard. */
  readonly username: string | null;
  readonly email: string | null;
}

export interface RegisterRequest {
  readonly username: string;
  readonly email: string;
  readonly password: string;
}

export interface LoginRequest {
  readonly usernameOrEmail: string;
  readonly password: string;
  readonly rememberMe: boolean;
}

/** A completed game submitted for verification + leaderboard ranking. */
export interface SubmitGameRequest {
  readonly variant: string;
  readonly seed: number;
  readonly level: number;
  readonly options: Readonly<Record<string, number>>;
  readonly moves: readonly MoveDto[];
  readonly claimedScore: number;
  readonly claimedTimeMs: number;
}

export interface SubmitGameResponse {
  readonly level: number;
  readonly score: number;
  readonly timeMs: number;
  readonly rank: number;
}

export interface LeaderboardRow {
  readonly rank: number;
  readonly username: string;
  readonly level: number;
  readonly score: number;
  readonly timeMs: number;
}

export interface LeaderboardResponse {
  readonly variant: string;
  readonly top: readonly LeaderboardRow[];
  /** The requesting player's rank, or null when signed out / unranked. */
  readonly playerRank: number | null;
  readonly playerBestLevel: number | null;
}

// -- Cross-device sync --------------------------------------------------------

/** A resumable game as it travels between a device and the account. */
export interface SyncSave {
  readonly variant: string;
  readonly level: number;
  readonly seed: number;
  readonly options: Record<string, number>;
  readonly moves: MoveDto[];
  readonly hintsUsed: number;
  readonly elapsedMs: number;
  /** Client save time (epoch ms); newest wins when two devices conflict. */
  readonly updatedAt: number;
}

export interface SyncProgress {
  readonly variant: string;
  readonly currentLevel: number;
}

export interface SyncStateResponse {
  readonly saves: SyncSave[];
  readonly progress: SyncProgress[];
}
