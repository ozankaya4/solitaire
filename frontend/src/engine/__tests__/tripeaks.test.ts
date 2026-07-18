// Focused unit tests for the TriPeaks TS port: the deal shape and the
// exposure / rank-adjacency rules — the parts most likely to drift when
// porting from C#. Broad cross-language parity is covered by vectors.test.ts.

import { describe, expect, it } from 'vitest';
import type { Card } from '../cards';
import {
  tripeaksGetLegalMoves,
  tripeaksIsWon,
  tripeaksNewGame,
  tripeaksTryApplyMove,
  TRIPEAKS_TABLEAU_SIZE,
  type TriPeaksState,
} from '../tripeaks';

const C = (suit: number, rank: number): Card => ({ suit, rank });

function state(overrides: {
  tableau?: Record<number, Card>;
  stock?: Card[];
  waste?: Card[];
  score?: number;
}): TriPeaksState {
  const tableau: (Card | undefined)[] = Array.from({ length: TRIPEAKS_TABLEAU_SIZE }, () => undefined);
  for (const [index, card] of Object.entries(overrides.tableau ?? {})) {
    tableau[Number(index)] = card;
  }
  return {
    tableau,
    stock: overrides.stock ?? [],
    waste: overrides.waste ?? [],
    score: overrides.score ?? 0,
  };
}

// Children table: 0->(3,4) 1->(5,6) 2->(7,8); 3->(9,10) 4->(10,11) 5->(12,13)
// 6->(13,14) 7->(15,16) 8->(16,17); 9->(18,19) 10->(19,20) 11->(20,21)
// 12->(21,22) 13->(22,23) 14->(23,24) 15->(24,25) 16->(25,26) 17->(26,27);
// 18-27 = base row, always exposed. Index 21 is shared: child of both 11
// (peak 0's last row) and 12 (peak 1's first row).

describe('TriPeaks deal', () => {
  it('deals 28 to the tableau, 1 to the waste, 23 to the stock, no duplicates', () => {
    const s = tripeaksNewGame(1);
    expect(s.tableau).toHaveLength(28);
    expect(s.tableau.every((c) => c !== undefined)).toBe(true);
    expect(s.waste).toHaveLength(1);
    expect(s.stock).toHaveLength(23);

    const seen = new Set<number>();
    for (const c of [...s.tableau, ...s.stock, ...s.waste]) {
      const ord = c!.suit * 13 + (c!.rank - 1);
      expect(seen.has(ord)).toBe(false);
      seen.add(ord);
    }
    expect(seen.size).toBe(52);
  });

  it('is deterministic for a seed and differs across seeds', () => {
    expect(tripeaksNewGame(42)).toEqual(tripeaksNewGame(42));
    expect(tripeaksNewGame(1).tableau[0]).not.toEqual(tripeaksNewGame(2).tableau[0]);
  });
});

describe('TriPeaks exposure', () => {
  it('the base row is exposed immediately', () => {
    const s = state({ tableau: { 20: C(3, 6) }, waste: [C(2, 5)] }); // 6 adjacent to 5
    const result = tripeaksTryApplyMove(s, { type: 'PlayToWaste', source: 20 });
    expect(result.ok).toBe(true);
    expect(result.scoreDelta).toBe(10);
  });

  it('a non-base card is not exposed while either child remains', () => {
    const blocked = state({
      tableau: { 0: C(3, 5), 3: C(2, 2), 4: C(0, 3) },
      waste: [C(1, 6)],
    });
    expect(tripeaksTryApplyMove(blocked, { type: 'PlayToWaste', source: 0 }).ok).toBe(false);

    const oneChildGone = state({ tableau: { 0: C(3, 5), 4: C(0, 3) }, waste: [C(1, 6)] });
    expect(tripeaksTryApplyMove(oneChildGone, { type: 'PlayToWaste', source: 0 }).ok).toBe(false);
  });

  it('becomes exposed once both children are gone', () => {
    const s = state({ tableau: { 0: C(3, 5) }, waste: [C(1, 6)] }); // children (3,4) already absent
    const result = tripeaksTryApplyMove(s, { type: 'PlayToWaste', source: 0 });
    expect(result.ok).toBe(true);
  });

  it('a shared base cell exposes both adjacent peaks independently', () => {
    const card11 = C(3, 5);
    const card12 = C(2, 5);
    const card20 = C(1, 2); // index 11's other child
    const card22 = C(0, 2); // index 12's other child
    // index 21 (shared) is always undefined in this test: already "removed".

    const neither = state({
      tableau: { 11: card11, 12: card12, 20: card20, 22: card22 },
      waste: [C(1, 4)],
    });
    expect(tripeaksTryApplyMove(neither, { type: 'PlayToWaste', source: 11 }).ok).toBe(false);
    expect(tripeaksTryApplyMove(neither, { type: 'PlayToWaste', source: 12 }).ok).toBe(false);

    // Only 20 gone: 11 is exposed (both its children, 20 and 21, are gone); 12 is not (22 remains).
    const expose11 = state({ tableau: { 11: card11, 12: card12, 22: card22 }, waste: [C(1, 4)] });
    expect(tripeaksTryApplyMove(expose11, { type: 'PlayToWaste', source: 11 }).ok).toBe(true);
    expect(tripeaksTryApplyMove(expose11, { type: 'PlayToWaste', source: 12 }).ok).toBe(false);

    // Only 22 gone: 12 is exposed; 11 is not (20 remains).
    const expose12 = state({ tableau: { 11: card11, 12: card12, 20: card20 }, waste: [C(1, 4)] });
    expect(tripeaksTryApplyMove(expose12, { type: 'PlayToWaste', source: 12 }).ok).toBe(true);
    expect(tripeaksTryApplyMove(expose12, { type: 'PlayToWaste', source: 11 }).ok).toBe(false);
  });
});

describe('TriPeaks rank adjacency', () => {
  it.each([
    [6, 5, true],
    [5, 6, true],
    [5, 7, false],
    [13, 1, true], // King -> Ace wraparound
    [1, 13, true], // Ace -> King wraparound
    [5, 5, false],
  ])('tableau rank %i vs waste rank %i -> legal=%s', (tableauRank, wasteRank, expectLegal) => {
    const s = state({ tableau: { 20: C(3, tableauRank) }, waste: [C(1, wasteRank)] });
    expect(tripeaksTryApplyMove(s, { type: 'PlayToWaste', source: 20 }).ok).toBe(expectLegal);
  });

  it('is illegal with an empty waste', () => {
    const s = state({ tableau: { 20: C(3, 7) } });
    expect(tripeaksTryApplyMove(s, { type: 'PlayToWaste', source: 20 }).ok).toBe(false);
  });
});

describe('TriPeaks stock/waste', () => {
  it('draw moves the stock top to the waste', () => {
    const s = state({ stock: [C(3, 4), C(3, 5)], waste: [C(1, 9)] });
    const result = tripeaksTryApplyMove(s, { type: 'Draw' });
    expect(result.ok).toBe(true);
    expect(result.next.waste).toEqual([C(1, 9), C(3, 4)]);
    expect(result.next.stock).toHaveLength(1);
  });

  it('recycle requires an empty stock, and is unlimited', () => {
    const s = state({ waste: [C(3, 4), C(3, 5)] });
    const recycled = tripeaksTryApplyMove(s, { type: 'Recycle' });
    expect(recycled.ok).toBe(true);
    expect(recycled.next.stock).toEqual([C(3, 4), C(3, 5)]);
    expect(recycled.next.waste).toHaveLength(0);

    const d1 = tripeaksTryApplyMove(recycled.next, { type: 'Draw' });
    const d2 = tripeaksTryApplyMove(d1.next, { type: 'Draw' });
    expect(tripeaksTryApplyMove(d2.next, { type: 'Recycle' }).ok).toBe(true);
  });

  it('recycle with a non-empty stock is illegal', () => {
    const s = state({ stock: [C(3, 4)], waste: [C(3, 5)] });
    expect(tripeaksTryApplyMove(s, { type: 'Recycle' }).ok).toBe(false);
  });
});

describe('TriPeaks win detection', () => {
  it('is won once the whole tableau is cleared, regardless of stock/waste', () => {
    const cleared = state({ stock: [C(0, 2)], waste: [C(1, 9)] });
    expect(tripeaksIsWon(cleared)).toBe(true);

    const notCleared = state({ tableau: { 20: C(1, 2) } });
    expect(tripeaksIsWon(notCleared)).toBe(false);
  });
});

describe('TriPeaks legal moves + replay', () => {
  it('offers at least one move from a fresh deal', () => {
    expect(tripeaksGetLegalMoves(tripeaksNewGame(1)).length).toBeGreaterThan(0);
  });

  it('replay stops at the first illegal move', () => {
    const moves = [{ type: 'Draw' }, { type: 'Recycle' }]; // recycle w/ non-empty stock
    const result = tripeaksTryApplyMove(tripeaksNewGame(3), moves[0]!);
    expect(result.ok).toBe(true);
    const second = tripeaksTryApplyMove(result.next, moves[1]!);
    expect(second.ok).toBe(false);
  });
});
