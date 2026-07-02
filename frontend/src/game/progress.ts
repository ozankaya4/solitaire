// Persistence of the player's current level per variant. Abstracted behind a
// store interface so tests use an in-memory store and the app uses localStorage.

export interface ProgressStore {
  getLevel(variant: string): number | undefined;
  setLevel(variant: string, level: number): void;
}

const KEY_PREFIX = 'solitaire:level:';

export function createMemoryProgressStore(
  initial?: Readonly<Record<string, number>>,
): ProgressStore {
  const map = new Map<string, number>(Object.entries(initial ?? {}));
  return {
    getLevel: (variant) => map.get(variant),
    setLevel: (variant, level) => {
      map.set(variant, level);
    },
  };
}

/**
 * Backed by a Web Storage (localStorage by default). Falls back to an in-memory
 * store when no storage is available (e.g. SSR or tests without a DOM).
 */
export function createLocalStorageProgressStore(storage?: Storage): ProgressStore {
  const store = storage ?? (typeof localStorage !== 'undefined' ? localStorage : undefined);
  if (store === undefined) {
    return createMemoryProgressStore();
  }
  return {
    getLevel: (variant) => {
      const raw = store.getItem(KEY_PREFIX + variant);
      if (raw === null) {
        return undefined;
      }
      const value = Number.parseInt(raw, 10);
      return Number.isFinite(value) ? value : undefined;
    },
    setLevel: (variant, level) => {
      store.setItem(KEY_PREFIX + variant, String(level));
    },
  };
}

/** Current level for a variant (defaults to 1 if none stored). */
export function getCurrentLevel(store: ProgressStore, variant: string): number {
  return store.getLevel(variant) ?? 1;
}

export function setCurrentLevel(store: ProgressStore, variant: string, level: number): void {
  store.setLevel(variant, Math.max(1, Math.floor(level)));
}

/** Advances the stored level for a variant by one and returns the new level. */
export function advanceLevel(store: ProgressStore, variant: string): number {
  const next = getCurrentLevel(store, variant) + 1;
  setCurrentLevel(store, variant, next);
  return next;
}
