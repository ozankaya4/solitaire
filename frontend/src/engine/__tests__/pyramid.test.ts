// Focused unit tests for the Pyramid TS port: the deal shape and the exposure /
// pairing rules — the parts most likely to drift when porting from C#. Broad
// cross-language parity is covered by vectors.test.ts.

import { describe, expect, it } from 'vitest';
import type { Card } from '../cards';
import {
  pyramidGetLegalMoves,
  pyramidIsWon,
  pyramidNewGame,
  pyramidTryApplyMove,
  PYRAMID_SIZE,
  PYRAMID_WASTE,
  type PyramidState,
} from '../pyramid';

const C = (suit: number, rank: number): Card => ({ suit, rank });

function state(overrides: {
  pyramid?: Record<number, Card>;
  stock?: Card[];
  waste?: Card[];
  score?: number;
}): PyramidState {
  const pyramid: (Card | undefined)[] = Array.from({ length: PYRAMID_SIZE }, () => undefined);
  for (const [index, card] of Object.entries(overrides.pyramid ?? {})) {
    pyramid[Number(index)] = card;
  }
  return {
    pyramid,
    stock: overrides.stock ?? [],
    waste: overrides.waste ?? [],
    score: overrides.score ?? 0,
  };
}

// Flat indices: row0=0; row1=1,2; row2=3,4,5; row3=6..9; row4=10..14;
// row5=15..20; row6(base)=21..27. Index 0's children are 1 and 2.

describe('Pyramid deal', () => {
  it('deals 28 cards to the pyramid and 24 to the stock, no duplicates', () => {
    const s = pyramidNewGame(1);
    expect(s.pyramid).toHaveLength(28);
    expect(s.pyramid.every((c) => c !== undefined)).toBe(true);
    expect(s.stock).toHaveLength(24);
    expect(s.waste).toHaveLength(0);

    const seen = new Set<number>();
    for (const c of [...s.pyramid, ...s.stock]) {
      const ord = c!.suit * 13 + (c!.rank - 1);
      expect(seen.has(ord)).toBe(false);
      seen.add(ord);
    }
    expect(seen.size).toBe(52);
  });

  it('is deterministic for a seed and differs across seeds', () => {
    expect(pyramidNewGame(42)).toEqual(pyramidNewGame(42));
    expect(pyramidNewGame(1).pyramid[0]).not.toEqual(pyramidNewGame(2).pyramid[0]);
  });
});

describe('Pyramid exposure', () => {
  it('the base row is exposed immediately, nothing else needed', () => {
    const s = state({ pyramid: { 27: C(3, 13) } }); // last base-row slot, a King
    const result = pyramidTryApplyMove(s, { type: 'RemoveSingle', source: 27 });
    expect(result.ok).toBe(true);
    expect(result.scoreDelta).toBe(10);
  });

  it('a non-base card is not exposed while either child remains', () => {
    const blocked = state({ pyramid: { 0: C(3, 13), 1: C(2, 5), 2: C(0, 6) } });
    expect(pyramidTryApplyMove(blocked, { type: 'RemoveSingle', source: 0 }).ok).toBe(false);

    const oneChildGone = state({ pyramid: { 0: C(3, 13), 2: C(0, 6) } }); // only index 1 gone
    expect(pyramidTryApplyMove(oneChildGone, { type: 'RemoveSingle', source: 0 }).ok).toBe(false);
  });

  it('becomes exposed once both children are gone', () => {
    const s = state({ pyramid: { 0: C(3, 13) } }); // children (1,2) already absent
    const result = pyramidTryApplyMove(s, { type: 'RemoveSingle', source: 0 });
    expect(result.ok).toBe(true);
  });
});

describe('Pyramid pairing', () => {
  it('removes a pair summing to 13, scoring 15', () => {
    const s = state({ pyramid: { 26: C(2, 9), 27: C(0, 4) } }); // 9 + 4
    const result = pyramidTryApplyMove(s, { type: 'RemovePair', source: 26, destination: 27 });
    expect(result.ok).toBe(true);
    expect(result.scoreDelta).toBe(15);
    expect(result.next.pyramid[26]).toBeUndefined();
    expect(result.next.pyramid[27]).toBeUndefined();
  });

  it('rejects a pair that does not sum to 13', () => {
    const s = state({ pyramid: { 26: C(2, 9), 27: C(0, 5) } }); // 9 + 5 = 14
    expect(pyramidTryApplyMove(s, { type: 'RemovePair', source: 26, destination: 27 }).ok).toBe(false);
  });

  it('pairs an exposed pyramid card with the waste top', () => {
    const s = state({ pyramid: { 27: C(2, 6) }, waste: [C(0, 7)] }); // 6 + 7
    const result = pyramidTryApplyMove(s, {
      type: 'RemovePair',
      source: 27,
      destination: PYRAMID_WASTE,
    });
    expect(result.ok).toBe(true);
    expect(result.next.waste).toHaveLength(0);
  });

  it('removes a lone King from the waste top', () => {
    const s = state({ waste: [C(1, 13)] });
    const result = pyramidTryApplyMove(s, { type: 'RemoveSingle', source: PYRAMID_WASTE });
    expect(result.ok).toBe(true);
    expect(result.scoreDelta).toBe(10);
  });

  it('cannot pair the waste top with itself', () => {
    const s = state({ waste: [C(1, 6)] });
    expect(
      pyramidTryApplyMove(s, { type: 'RemovePair', source: PYRAMID_WASTE, destination: PYRAMID_WASTE })
        .ok,
    ).toBe(false);
  });
});

describe('Pyramid stock/waste', () => {
  it('draw moves the stock top to the waste', () => {
    const s = state({ stock: [C(3, 4), C(3, 5)] });
    const result = pyramidTryApplyMove(s, { type: 'Draw' });
    expect(result.ok).toBe(true);
    expect(result.next.waste).toEqual([C(3, 4)]);
    expect(result.next.stock).toHaveLength(1);
  });

  it('recycle requires an empty stock, and is unlimited', () => {
    const s = state({ waste: [C(3, 4), C(3, 5)] });
    const recycled = pyramidTryApplyMove(s, { type: 'Recycle' });
    expect(recycled.ok).toBe(true);
    expect(recycled.next.stock).toEqual([C(3, 4), C(3, 5)]);
    expect(recycled.next.waste).toHaveLength(0);

    // Draw both back out and recycle again — no redeal limit.
    const d1 = pyramidTryApplyMove(recycled.next, { type: 'Draw' });
    const d2 = pyramidTryApplyMove(d1.next, { type: 'Draw' });
    expect(pyramidTryApplyMove(d2.next, { type: 'Recycle' }).ok).toBe(true);
  });

  it('recycle with a non-empty stock is illegal', () => {
    const s = state({ stock: [C(3, 4)], waste: [C(3, 5)] });
    expect(pyramidTryApplyMove(s, { type: 'Recycle' }).ok).toBe(false);
  });
});

describe('Pyramid win detection', () => {
  it('is won once the whole triangle is cleared, regardless of stock/waste', () => {
    const cleared = state({ stock: [C(0, 2)], waste: [C(1, 9)] });
    expect(pyramidIsWon(cleared)).toBe(true);

    const notCleared = state({ pyramid: { 27: C(1, 2) } });
    expect(pyramidIsWon(notCleared)).toBe(false);
  });
});

describe('Pyramid legal moves + replay', () => {
  it('offers at least one move from a fresh deal', () => {
    expect(pyramidGetLegalMoves(pyramidNewGame(1)).length).toBeGreaterThan(0);
  });

  it('replay stops at the first illegal move', () => {
    const moves = [{ type: 'Draw' }, { type: 'Recycle' }]; // recycle w/ non-empty stock
    const result = pyramidTryApplyMove(pyramidNewGame(3), moves[0]!);
    expect(result.ok).toBe(true);
    const second = pyramidTryApplyMove(result.next, moves[1]!);
    expect(second.ok).toBe(false);
  });
});
