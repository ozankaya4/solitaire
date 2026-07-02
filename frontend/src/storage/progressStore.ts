import type { ProgressStore } from '../game/progress';
import { getStoredLevel, setStoredLevel } from './cache';

/** ProgressStore backed by the IndexedDB-hydrated cache. */
export function createCacheProgressStore(): ProgressStore {
  return {
    getLevel: (variant) => getStoredLevel(variant),
    setLevel: (variant, level) => setStoredLevel(variant, level),
  };
}
