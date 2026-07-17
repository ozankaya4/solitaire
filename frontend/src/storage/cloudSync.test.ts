import { describe, expect, it } from 'vitest';
import type { SyncStateResponse } from '../api/types';
import type { SavedGame } from './types';
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

const empty: SyncStateResponse = { saves: [], progress: [] };

describe('planMerge — saves (newest updatedAt wins)', () => {
  it('adopts a server save that is newer than local', () => {
    const plan = planMerge([localSave('klondike', 100)], {}, { saves: [serverSave('klondike', 200)], progress: [] });
    expect(plan.adoptSaves.map((s) => s.variant)).toEqual(['klondike']);
    expect(plan.pushSaves).toEqual([]);
  });

  it('pushes a local save that is newer than the server', () => {
    const plan = planMerge([localSave('klondike', 300)], {}, { saves: [serverSave('klondike', 200)], progress: [] });
    expect(plan.pushSaves).toEqual(['klondike']);
    expect(plan.adoptSaves).toEqual([]);
  });

  it('pushes a save that exists only locally', () => {
    const plan = planMerge([localSave('spider', 50)], {}, empty);
    expect(plan.pushSaves).toEqual(['spider']);
  });

  it('adopts a save that exists only on the server', () => {
    const plan = planMerge([], {}, { saves: [serverSave('spider', 50)], progress: [] });
    expect(plan.adoptSaves.map((s) => s.variant)).toEqual(['spider']);
  });

  it('does nothing when timestamps are equal', () => {
    const plan = planMerge([localSave('klondike', 100)], {}, { saves: [serverSave('klondike', 100)], progress: [] });
    expect(plan.adoptSaves).toEqual([]);
    expect(plan.pushSaves).toEqual([]);
  });
});

describe('planMerge — progress (highest level wins)', () => {
  it('adopts server progress that is ahead', () => {
    const plan = planMerge([], { klondike: 3 }, { saves: [], progress: [{ variant: 'klondike', currentLevel: 9 }] });
    expect(plan.adoptProgress).toEqual([{ variant: 'klondike', currentLevel: 9 }]);
    expect(plan.pushProgress).toEqual([]);
  });

  it('pushes local progress that is ahead', () => {
    const plan = planMerge([], { klondike: 12 }, { saves: [], progress: [{ variant: 'klondike', currentLevel: 4 }] });
    expect(plan.pushProgress).toEqual(['klondike']);
    expect(plan.adoptProgress).toEqual([]);
  });

  it('treats a missing side as level 1', () => {
    // Local has spider at 5, server has never heard of spider (=> treated as 1).
    const plan = planMerge([], { spider: 5 }, empty);
    expect(plan.pushProgress).toEqual(['spider']);
  });
});
