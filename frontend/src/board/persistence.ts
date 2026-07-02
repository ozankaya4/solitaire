// Saved-game persistence. Session rule: a finished game that is exited is NOT
// persisted; an unfinished game that is exited is saved (per variant) and resumed
// on the next entry into that variant.

import type { MoveDto } from '../engine/types';
import type { VariantId } from '../app/types';

export interface SavedGame {
  readonly variant: VariantId;
  readonly level: number;
  readonly seed: number;
  readonly bag: Record<string, number>;
  readonly moves: MoveDto[];
  readonly hintsUsed: number;
}

const keyFor = (variant: VariantId): string => `solitaire:save:${variant}`;

export function loadSavedGame(variant: VariantId): SavedGame | null {
  if (typeof localStorage === 'undefined') {
    return null;
  }
  try {
    const raw = localStorage.getItem(keyFor(variant));
    return raw === null ? null : (JSON.parse(raw) as SavedGame);
  } catch {
    return null;
  }
}

export function saveGame(game: SavedGame): void {
  if (typeof localStorage === 'undefined') {
    return;
  }
  try {
    localStorage.setItem(keyFor(game.variant), JSON.stringify(game));
  } catch {
    /* storage unavailable */
  }
}

export function clearSavedGame(variant: VariantId): void {
  if (typeof localStorage === 'undefined') {
    return;
  }
  try {
    localStorage.removeItem(keyFor(variant));
  } catch {
    /* storage unavailable */
  }
}
