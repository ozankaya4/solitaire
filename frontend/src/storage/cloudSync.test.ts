import { describe, expect, it } from 'vitest';
import type { SyncStateResponse } from '../api/types';
import type { SavedGame, VariantStats } from './types';
import { planMerge } from './cloudSync';

function localSave(variant: string, updatedAt: number): SavedGame {
  return {
    variant: variant as SavedGame['variant'],
    level: 1,
    seed: 1,
    bag: { drawCount: 1 },
    moves: [],
    hintsUsed: 0,
    elapsedMs: 0,
    updatedAt,
  };
}

function serverSave(variant: string, updatedAt: number): SyncStateResponse['saves'][number] {
  return {
    variant,
    level: 1,
    seed: 1,
    options: { drawCount: 1 },
    moves: [],
    hintsUsed: 0,
    elapsedMs: 0,
    updatedAt,
  };
}

function serverStat(
  variant: string,
  gamesPlayed: number,
  wins: number,
  bestTimeMs: number | null = null,
): SyncStateResponse['stats'][number] {
  return { variant, gamesPlayed, wins, bestTimeMs };
}

function stats(gamesPlayed: number, wins: number, bestTimeMs: number | null = null): VariantStats {
  return { gamesPlayed, wins, bestTimeMs };
}

/** Builds a server state, defaulting the arms the test isn't exercising to empty. */
function server(partial: Partial<SyncStateResponse>): SyncStateResponse {
  return { saves: [], progress: [], stats: [], ...partial };
}

const empty: SyncStateResponse = server({});

describe('planMerge — saves (newest updatedAt wins)', () => {
  it('adopts a server save that is newer than local', () => {
    const plan = planMerge([localSave('klondike', 100)], {}, {}, server({ saves: [serverSave('klondike', 200)] }));
    expect(plan.adoptSaves.map((s) => s.variant)).toEqual(['klondike']);
    expect(plan.pushSaves).toEqual([]);
  });

  it('pushes a local save that is newer than the server', () => {
    const plan = planMerge([localSave('klondike', 300)], {}, {}, server({ saves: [serverSave('klondike', 200)] }));
    expect(plan.pushSaves).toEqual(['klondike']);
    expect(plan.adoptSaves).toEqual([]);
  });

  it('pushes a save that exists only locally', () => {
    const plan = planMerge([localSave('spider', 50)], {}, {}, empty);
    expect(plan.pushSaves).toEqual(['spider']);
  });

  it('adopts a save that exists only on the server', () => {
    const plan = planMerge([], {}, {}, server({ saves: [serverSave('spider', 50)] }));
    expect(plan.adoptSaves.map((s) => s.variant)).toEqual(['spider']);
  });

  it('does nothing when timestamps are equal', () => {
    const plan = planMerge([localSave('klondike', 100)], {}, {}, server({ saves: [serverSave('klondike', 100)] }));
    expect(plan.adoptSaves).toEqual([]);
    expect(plan.pushSaves).toEqual([]);
  });
});

describe('planMerge — progress (highest level wins)', () => {
  it('adopts server progress that is ahead', () => {
    const plan = planMerge([], { klondike: 3 }, {}, server({ progress: [{ variant: 'klondike', currentLevel: 9 }] }));
    expect(plan.adoptProgress).toEqual([{ variant: 'klondike', currentLevel: 9 }]);
    expect(plan.pushProgress).toEqual([]);
  });

  it('pushes local progress that is ahead', () => {
    const plan = planMerge([], { klondike: 12 }, {}, server({ progress: [{ variant: 'klondike', currentLevel: 4 }] }));
    expect(plan.pushProgress).toEqual(['klondike']);
    expect(plan.adoptProgress).toEqual([]);
  });

  it('treats a missing side as level 1', () => {
    // Local has spider at 5, server has never heard of spider (=> treated as 1).
    const plan = planMerge([], { spider: 5 }, {}, empty);
    expect(plan.pushProgress).toEqual(['spider']);
  });
});

describe('planMerge — stats (per-field monotonic merge)', () => {
  it('adopts server stats when the server is ahead and local has none', () => {
    const plan = planMerge([], {}, {}, server({ stats: [serverStat('klondike', 13, 8, 210_000)] }));
    expect(plan.adoptStats).toEqual([
      { variant: 'klondike', gamesPlayed: 13, wins: 8, bestTimeMs: 210_000 },
    ]);
    expect(plan.pushStats).toEqual([]);
  });

  it('pushes local stats the server is missing', () => {
    const plan = planMerge([], {}, { spider: stats(4, 1, 300_000) }, empty);
    expect(plan.pushStats).toEqual(['spider']);
    expect(plan.adoptStats).toEqual([]);
  });

  it('converges both sides when each leads a different field', () => {
    // Local: more games; server: more wins AND a faster best time.
    const plan = planMerge(
      [],
      {},
      { klondike: stats(20, 5, 300_000) },
      server({ stats: [serverStat('klondike', 10, 8, 200_000)] }),
    );
    // The merged value climbs on every field; both sides adopt/receive it.
    expect(plan.adoptStats).toEqual([
      { variant: 'klondike', gamesPlayed: 20, wins: 8, bestTimeMs: 200_000 },
    ]);
    expect(plan.pushStats).toEqual(['klondike']);
  });

  it('does nothing when both sides already match', () => {
    const plan = planMerge(
      [],
      {},
      { klondike: stats(13, 8, 210_000) },
      server({ stats: [serverStat('klondike', 13, 8, 210_000)] }),
    );
    expect(plan.adoptStats).toEqual([]);
    expect(plan.pushStats).toEqual([]);
  });

  it('takes the non-null best time when only one side has won', () => {
    const plan = planMerge(
      [],
      {},
      { klondike: stats(5, 0, null) },
      server({ stats: [serverStat('klondike', 5, 1, 250_000)] }),
    );
    // Server has the win + best time; local adopts the merged (its own games count matches).
    expect(plan.adoptStats).toEqual([
      { variant: 'klondike', gamesPlayed: 5, wins: 1, bestTimeMs: 250_000 },
    ]);
    expect(plan.pushStats).toEqual([]);
  });
});
