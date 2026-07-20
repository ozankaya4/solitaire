// Cross-device sync for signed-in players. Local-first: the device owns the data
// and the server is a durable mirror. On sign-in we reconcile (newest save wins,
// highest level wins), then push local changes up as they happen (debounced).
// We also re-reconcile whenever the app regains focus/visibility, so a game
// saved on one device shows up on another as soon as you return to it — without
// this, a device pulls only once per load and mobile PWAs (which resume from a
// frozen state rather than reloading) would keep showing stale saves.
//
// The merge decision is a pure function (see `planMerge`) so it can be tested
// without a network or a store; this file's side-effecting parts just carry the
// plan out and wire it to auth + store changes.

import { api, withRetry } from '../api/client';
import type { SyncProgress, SyncSave, SyncStat, SyncStateResponse } from '../api/types';
import type { VariantId } from '../app/types';
import type { SavedGame, VariantStats } from './types';
import {
  applyRemoteProgress,
  applyRemoteSave,
  applyRemoteStats,
  clearGameData,
  getAllProgress,
  getAllStats,
  getSave,
  getStats,
  getStoredLevel,
  listSaves,
  subscribeStore,
  type StoreChange,
} from './cache';

// -- conversions --------------------------------------------------------------

function toSyncSave(local: SavedGame): SyncSave {
  return {
    variant: local.variant,
    level: local.level,
    seed: local.seed,
    options: local.bag,
    moves: [...local.moves],
    hintsUsed: local.hintsUsed,
    elapsedMs: local.elapsedMs ?? 0,
    updatedAt: local.updatedAt,
  };
}

function toLocalSave(remote: SyncSave): SavedGame {
  return {
    variant: remote.variant as VariantId,
    level: remote.level,
    seed: remote.seed,
    bag: remote.options,
    moves: [...remote.moves],
    hintsUsed: remote.hintsUsed,
    elapsedMs: remote.elapsedMs,
    updatedAt: remote.updatedAt,
  };
}

const ZERO_STATS: VariantStats = { gamesPlayed: 0, wins: 0, bestTimeMs: null };

/** Monotonic per-field merge: counts climb (max), best time drops (min). */
function mergeStats(a: VariantStats, b: VariantStats): VariantStats {
  const bestTimeMs =
    a.bestTimeMs === null
      ? b.bestTimeMs
      : b.bestTimeMs === null
        ? a.bestTimeMs
        : Math.min(a.bestTimeMs, b.bestTimeMs);
  return {
    gamesPlayed: Math.max(a.gamesPlayed, b.gamesPlayed),
    wins: Math.max(a.wins, b.wins),
    bestTimeMs,
  };
}

function statsEqual(a: VariantStats, b: VariantStats): boolean {
  return a.gamesPlayed === b.gamesPlayed && a.wins === b.wins && a.bestTimeMs === b.bestTimeMs;
}

function toSyncStat(variant: string, s: VariantStats): SyncStat {
  return { variant, gamesPlayed: s.gamesPlayed, wins: s.wins, bestTimeMs: s.bestTimeMs };
}

function toLocalStats(remote: SyncStat): VariantStats {
  return { gamesPlayed: remote.gamesPlayed, wins: remote.wins, bestTimeMs: remote.bestTimeMs };
}

// -- pure merge planning (unit-tested) ----------------------------------------

export interface MergePlan {
  /** Server saves that are newer than local → adopt locally. */
  readonly adoptSaves: SyncSave[];
  /** Local save variants that are newer than the server → push up. */
  readonly pushSaves: string[];
  /** Server progress that is ahead of local → adopt locally. */
  readonly adoptProgress: SyncProgress[];
  /** Local progress variants that are ahead of the server → push up. */
  readonly pushProgress: string[];
  /** Merged stats to write locally (server had something local didn't). */
  readonly adoptStats: SyncStat[];
  /** Stat variants whose merged value the server is missing → push up. */
  readonly pushStats: string[];
}

/**
 * Decides, for each variant, which side is authoritative. Saves: the newer
 * `updatedAt` wins. Progress: the higher level wins (monotonic). Stats: a
 * per-field monotonic merge (highest counts, lowest best time) — so both sides
 * converge and re-syncing never double-counts. Variants present on only one side
 * flow to the other.
 */
export function planMerge(
  localSaves: readonly SavedGame[],
  localProgress: Readonly<Record<string, number>>,
  localStats: Readonly<Record<string, VariantStats>>,
  server: SyncStateResponse,
): MergePlan {
  const adoptSaves: SyncSave[] = [];
  const pushSaves: string[] = [];
  const localSaveByVariant = new Map<string, SavedGame>(localSaves.map((s) => [s.variant, s]));
  const serverSaveByVariant = new Map<string, SyncSave>(server.saves.map((s) => [s.variant, s]));

  for (const variant of new Set([...localSaveByVariant.keys(), ...serverSaveByVariant.keys()])) {
    const local = localSaveByVariant.get(variant);
    const remote = serverSaveByVariant.get(variant);
    if (remote && (!local || remote.updatedAt > local.updatedAt)) {
      adoptSaves.push(remote);
    } else if (local && (!remote || local.updatedAt > remote.updatedAt)) {
      pushSaves.push(variant);
    }
  }

  const adoptProgress: SyncProgress[] = [];
  const pushProgress: string[] = [];
  const serverLevel = new Map(server.progress.map((p) => [p.variant, p.currentLevel]));
  for (const variant of new Set([...Object.keys(localProgress), ...serverLevel.keys()])) {
    const local = localProgress[variant] ?? 1;
    const remote = serverLevel.get(variant) ?? 1;
    if (remote > local) {
      adoptProgress.push({ variant, currentLevel: remote });
    } else if (local > remote) {
      pushProgress.push(variant);
    }
  }

  const adoptStats: SyncStat[] = [];
  const pushStats: string[] = [];
  const serverStat = new Map(server.stats.map((s) => [s.variant, toLocalStats(s)]));
  for (const variant of new Set([...Object.keys(localStats), ...serverStat.keys()])) {
    const local = localStats[variant] ?? ZERO_STATS;
    const remote = serverStat.get(variant) ?? ZERO_STATS;
    const merged = mergeStats(local, remote);
    // Adopt if local isn't already the merged value; push if the server isn't.
    if (!statsEqual(merged, local)) {
      adoptStats.push(toSyncStat(variant, merged));
    }
    if (!statsEqual(merged, remote)) {
      pushStats.push(variant);
    }
  }

  return { adoptSaves, pushSaves, adoptProgress, pushProgress, adoptStats, pushStats };
}

// -- controller ---------------------------------------------------------------

const OWNER_KEY = 'solitaire:sync-owner';
const FLUSH_DELAY_MS = 1500;
// A focus/visibility re-sync no more often than this — switching tabs quickly
// (and the server's "sync" rate limit) shouldn't trigger a burst of pulls.
const RESYNC_THROTTLE_MS = 8000;

let active = false;
let unsubscribe: (() => void) | null = null;
let flushTimer: ReturnType<typeof setTimeout> | null = null;
let reconciling = false;
let lastReconcileAt = 0;
const dirtySaves = new Set<string>();
const dirtyProgress = new Set<string>();
const dirtyStats = new Set<string>();

function readOwner(): string | null {
  try {
    return localStorage.getItem(OWNER_KEY);
  } catch {
    return null;
  }
}

function writeOwner(userId: string): void {
  try {
    localStorage.setItem(OWNER_KEY, userId);
  } catch {
    /* storage unavailable */
  }
}

/** Begin syncing for the signed-in user. Safe to call more than once. */
export function startCloudSync(userId: string): void {
  if (active) {
    return;
  }
  active = true;

  // Never let one account adopt another account's local games on a shared device.
  // A null owner means the local data is unclaimed guest data — adopt it (upload),
  // don't wipe it. Only a *different* known account triggers a clear.
  const owner = readOwner();
  if (owner !== null && owner !== userId) {
    clearGameData();
  }
  writeOwner(userId);

  unsubscribe = subscribeStore(onStoreChange);
  if (typeof document !== 'undefined') {
    document.addEventListener('visibilitychange', onVisibilityChange);
  }
  if (typeof window !== 'undefined') {
    window.addEventListener('focus', maybeResync);
  }
  void reconcile();
}

/** Stop syncing (on sign-out). Local data stays; it just no longer mirrors up. */
export function stopCloudSync(): void {
  active = false;
  unsubscribe?.();
  unsubscribe = null;
  if (typeof document !== 'undefined') {
    document.removeEventListener('visibilitychange', onVisibilityChange);
  }
  if (typeof window !== 'undefined') {
    window.removeEventListener('focus', maybeResync);
  }
  if (flushTimer) {
    clearTimeout(flushTimer);
    flushTimer = null;
  }
  dirtySaves.clear();
  dirtyProgress.clear();
  dirtyStats.clear();
}

function onVisibilityChange(): void {
  if (typeof document === 'undefined' || document.visibilityState === 'visible') {
    maybeResync();
  }
}

/** Re-pull from the server when the app returns to the foreground (throttled). */
function maybeResync(): void {
  if (!active) {
    return;
  }
  if (typeof document !== 'undefined' && document.visibilityState === 'hidden') {
    return;
  }
  if (Date.now() - lastReconcileAt < RESYNC_THROTTLE_MS) {
    return;
  }
  void reconcile();
}

async function reconcile(): Promise<void> {
  // One reconcile at a time; the throttle above rides on the timestamp this sets.
  if (reconciling) {
    return;
  }
  reconciling = true;
  try {
    let state: SyncStateResponse;
    try {
      // Retried: sign-in is often the session's first API touch (cold start).
      state = await withRetry(() => api.getSyncState(), 3, 3000);
    } catch {
      return; // offline / server down — local play is unaffected; retry next resync
    }
    if (!active) {
      return;
    }

    const plan = planMerge(listSaves(), getAllProgress(), getAllStats(), state);

    for (const remote of plan.adoptSaves) {
      applyRemoteSave(toLocalSave(remote));
    }
    for (const p of plan.adoptProgress) {
      applyRemoteProgress(p.variant, p.currentLevel);
    }
    // Adopt merged stats before pushing, so the push below sends the merged value.
    for (const remote of plan.adoptStats) {
      applyRemoteStats(remote.variant, toLocalStats(remote));
    }
    for (const variant of plan.pushSaves) {
      const local = getSave(variant as VariantId);
      if (local) {
        await api.putSave(toSyncSave(local)).catch(() => undefined);
      }
    }
    for (const variant of plan.pushProgress) {
      const level = getStoredLevel(variant);
      if (level != null) {
        await api.putProgress({ variant, currentLevel: level }).catch(() => undefined);
      }
    }
    for (const variant of plan.pushStats) {
      await api.putStats(toSyncStat(variant, getStats(variant as VariantId))).catch(() => undefined);
    }
  } finally {
    reconciling = false;
    lastReconcileAt = Date.now();
  }
}

function onStoreChange(change: StoreChange): void {
  // 'remote' writes came from the server (a pull) — never echo them back up.
  if (change.kind === 'remote' || !change.variant) {
    return;
  }
  if (change.kind === 'delete') {
    dirtySaves.delete(change.variant);
    void api.deleteRemoteSave(change.variant).catch(() => undefined);
    return;
  }
  if (change.kind === 'save') {
    dirtySaves.add(change.variant);
  } else if (change.kind === 'stats') {
    dirtyStats.add(change.variant);
  } else {
    dirtyProgress.add(change.variant);
  }
  scheduleFlush();
}

function scheduleFlush(): void {
  if (flushTimer) {
    return;
  }
  flushTimer = setTimeout(() => {
    flushTimer = null;
    void flush();
  }, FLUSH_DELAY_MS);
}

async function flush(): Promise<void> {
  for (const variant of [...dirtySaves]) {
    dirtySaves.delete(variant);
    const local = getSave(variant as VariantId);
    if (local) {
      await api.putSave(toSyncSave(local)).catch(() => undefined);
    }
  }
  for (const variant of [...dirtyProgress]) {
    dirtyProgress.delete(variant);
    const level = getStoredLevel(variant);
    if (level != null) {
      await api.putProgress({ variant, currentLevel: level }).catch(() => undefined);
    }
  }
  for (const variant of [...dirtyStats]) {
    dirtyStats.delete(variant);
    await api.putStats(toSyncStat(variant, getStats(variant as VariantId))).catch(() => undefined);
  }
}
