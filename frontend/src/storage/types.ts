import type { VariantId, ThemeName, Language, DrawMode } from '../app/types';
import type { MoveDto } from '../engine/types';

/** An unfinished, resumable game (one per variant). */
export interface SavedGame {
  variant: VariantId;
  level: number;
  seed: number;
  bag: Record<string, number>;
  moves: MoveDto[];
  hintsUsed: number;
  updatedAt: number;
}

export interface PersistedSettings {
  theme: ThemeName;
  defaultVariant: VariantId;
  language: Language;
  drawMode: DrawMode;
}

/** Lifetime, per-variant stats. Win rate is derived (wins / gamesPlayed). */
export interface VariantStats {
  gamesPlayed: number;
  wins: number;
  bestTimeMs: number | null;
}

export function emptyStats(): VariantStats {
  return { gamesPlayed: 0, wins: 0, bestTimeMs: null };
}
