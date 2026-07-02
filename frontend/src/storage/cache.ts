// In-memory snapshot of persisted data, hydrated from IndexedDB once at startup
// so the rest of the app can read synchronously. All writes update the snapshot
// immediately and mirror to IndexedDB (fire-and-forget). A one-time import from
// the previous localStorage layout runs on first hydration so nothing is lost.

import type { VariantId } from '../app/types';
import { idbDelete, idbGet, idbGetAll, idbGetAllKeys, idbPut, idbAvailable } from './db';
import { emptyStats, type PersistedSettings, type SavedGame, type VariantStats } from './types';

interface Snapshot {
  settings: PersistedSettings | null;
  progress: Record<string, number>;
  saves: Record<string, SavedGame>;
  stats: Record<string, VariantStats>;
}

const snapshot: Snapshot = { settings: null, progress: {}, saves: {}, stats: {} };
let hydrated = false;

async function loadMap<T>(store: 'progress' | 'saves' | 'stats'): Promise<Record<string, T>> {
  const [keys, values] = await Promise.all([idbGetAllKeys(store), idbGetAll<T>(store)]);
  const out: Record<string, T> = {};
  keys.forEach((key, i) => {
    const value = values[i];
    // All stores use string keys (variant ids / 'settings').
    if (value !== undefined && typeof key === 'string') {
      out[key] = value;
    }
  });
  return out;
}

export async function hydrateStore(): Promise<void> {
  if (hydrated) {
    return;
  }
  hydrated = true;
  if (!idbAvailable()) {
    importFromLocalStorage();
    return;
  }
  try {
    const [settings, progress, saves, stats] = await Promise.all([
      idbGet<PersistedSettings>('settings', 'settings'),
      loadMap<number>('progress'),
      loadMap<SavedGame>('saves'),
      loadMap<VariantStats>('stats'),
    ]);
    snapshot.settings = settings ?? null;
    snapshot.progress = progress;
    snapshot.saves = saves;
    snapshot.stats = stats;

    // First run with an empty DB → import any prior localStorage data.
    const empty =
      settings === undefined &&
      Object.keys(progress).length === 0 &&
      Object.keys(saves).length === 0;
    if (empty) {
      importFromLocalStorage();
      persistAll();
    }
  } catch {
    importFromLocalStorage();
  }
}

function importFromLocalStorage(): void {
  if (typeof localStorage === 'undefined') {
    return;
  }
  try {
    const rawSettings = localStorage.getItem('solitaire:settings');
    if (rawSettings !== null) {
      snapshot.settings = JSON.parse(rawSettings) as PersistedSettings;
    }
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key === null) {
        continue;
      }
      if (key.startsWith('solitaire:save:')) {
        const raw = localStorage.getItem(key);
        if (raw !== null) {
          const save = JSON.parse(raw) as SavedGame;
          save.updatedAt = save.updatedAt || Date.now();
          snapshot.saves[key.slice('solitaire:save:'.length)] = save;
        }
      } else if (key.startsWith('solitaire:level:')) {
        const raw = localStorage.getItem(key);
        if (raw !== null) {
          snapshot.progress[key.slice('solitaire:level:'.length)] = Number.parseInt(raw, 10);
        }
      }
    }
  } catch {
    /* ignore malformed legacy data */
  }
}

function persistAll(): void {
  if (snapshot.settings) {
    void idbPut('settings', 'settings', snapshot.settings).catch(() => undefined);
  }
  for (const [key, value] of Object.entries(snapshot.progress)) {
    void idbPut('progress', key, value).catch(() => undefined);
  }
  for (const [key, value] of Object.entries(snapshot.saves)) {
    void idbPut('saves', key, value).catch(() => undefined);
  }
  for (const [key, value] of Object.entries(snapshot.stats)) {
    void idbPut('stats', key, value).catch(() => undefined);
  }
}

// -- Settings -----------------------------------------------------------------

export function getPersistedSettings(): PersistedSettings | null {
  return snapshot.settings;
}

export function setPersistedSettings(settings: PersistedSettings): void {
  snapshot.settings = settings;
  void idbPut('settings', 'settings', settings).catch(() => undefined);
  // Mirror to localStorage so the pre-paint anti-FOUC script can read the theme.
  try {
    localStorage.setItem('solitaire:settings', JSON.stringify(settings));
  } catch {
    /* storage unavailable */
  }
}

// -- Progress (current level per variant) -------------------------------------

export function getStoredLevel(variant: string): number | undefined {
  return snapshot.progress[variant];
}

export function setStoredLevel(variant: string, level: number): void {
  snapshot.progress[variant] = level;
  void idbPut('progress', variant, level).catch(() => undefined);
}

// -- Saved games --------------------------------------------------------------

export function getSave(variant: VariantId): SavedGame | null {
  return snapshot.saves[variant] ?? null;
}

export function listSaves(): SavedGame[] {
  return Object.values(snapshot.saves).sort((a, b) => b.updatedAt - a.updatedAt);
}

export function putSave(save: SavedGame): void {
  snapshot.saves[save.variant] = save;
  void idbPut('saves', save.variant, save).catch(() => undefined);
}

export function deleteSave(variant: VariantId): void {
  delete snapshot.saves[variant];
  void idbDelete('saves', variant).catch(() => undefined);
}

// -- Stats --------------------------------------------------------------------

export function getStats(variant: VariantId): VariantStats {
  return snapshot.stats[variant] ?? emptyStats();
}

function writeStats(variant: VariantId, stats: VariantStats): void {
  snapshot.stats[variant] = stats;
  void idbPut('stats', variant, stats).catch(() => undefined);
}

export function recordGameStarted(variant: VariantId): void {
  const stats = getStats(variant);
  writeStats(variant, { ...stats, gamesPlayed: stats.gamesPlayed + 1 });
}

export function recordWin(variant: VariantId, timeMs: number): void {
  const stats = getStats(variant);
  const bestTimeMs = stats.bestTimeMs === null ? timeMs : Math.min(stats.bestTimeMs, timeMs);
  writeStats(variant, { ...stats, wins: stats.wins + 1, bestTimeMs });
}
