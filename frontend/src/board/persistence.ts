// Saved-game persistence, backed by the IndexedDB cache. Session rule: a finished
// game that is exited is NOT persisted; an unfinished game that is exited is
// saved (one per variant) and resumed on the next entry into that variant.

import type { VariantId } from '../app/types';
import { deleteSave, getSave, putSave } from '../storage/cache';
import type { SavedGame } from '../storage/types';

export type { SavedGame };

export function loadSavedGame(variant: VariantId): SavedGame | null {
  return getSave(variant);
}

export function saveGame(game: Omit<SavedGame, 'updatedAt'>): void {
  putSave({ ...game, updatedAt: Date.now() });
}

export function clearSavedGame(variant: VariantId): void {
  deleteSave(variant);
}
