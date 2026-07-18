// Focused unit tests that pin the pieces the cross-language contract relies on:
// the exact PRNG output, deterministic deals, and the conceptual engine API.

import { describe, expect, it } from 'vitest';
import { buildOrdered, dealKlondike, shuffle } from '../deck';
import { solitaireEngines } from '../engines';
import {
  klondikeGetLegalMoves,
  klondikeIsWon,
  klondikeNewGame,
  klondikeTryApplyMove,
} from '../klondike';
import { DeterministicRandom } from '../random';
import { spiderNewGame, spiderTryApplyMove } from '../spider';

describe('DeterministicRandom', () => {
  it('reproduces the C# mulberry32 known-answer for seed 1', () => {
    const rng = new DeterministicRandom(1);
    const expected = [2693262067, 11749833, 2265367787, 4213581821, 4159151403];
    for (const value of expected) {
      expect(rng.nextUint32()).toBe(value);
    }
  });

  it('keeps nextInt within bounds', () => {
    const rng = new DeterministicRandom(999);
    for (let i = 0; i < 10000; i++) {
      const v = rng.nextInt(52);
      expect(v).toBeGreaterThanOrEqual(0);
      expect(v).toBeLessThan(52);
    }
  });
});

describe('Deck', () => {
  it('builds 52 distinct cards in canonical order', () => {
    const deck = buildOrdered();
    expect(deck).toHaveLength(52);
    expect(new Set(deck.map((c) => c.suit * 13 + c.rank)).size).toBe(52);
  });

  it('shuffles deterministically for a seed', () => {
    expect(shuffle(2024)).toEqual(shuffle(2024));
  });

  it('deals the standard Klondike layout', () => {
    const { tableau, stock } = dealKlondike(shuffle(2024));
    expect(tableau).toHaveLength(7);
    tableau.forEach((pile, c) => {
      expect(pile.cards).toHaveLength(c + 1);
      expect(pile.faceDownCount).toBe(c);
    });
    expect(stock).toHaveLength(24);
  });
});

describe('Klondike conceptual API', () => {
  it('starts a fresh game and offers a draw', () => {
    const state = klondikeNewGame(1, { drawCount: 1, maxRedeals: 2147483647 });
    expect(state.score).toBe(0);
    expect(klondikeIsWon(state)).toBe(false);
    expect(klondikeGetLegalMoves(state).some((m) => m.type === 'Draw')).toBe(true);
  });

  it('applies a legal draw and rejects an illegal recycle', () => {
    const state = klondikeNewGame(1, { drawCount: 1, maxRedeals: 2147483647 });
    const drawn = klondikeTryApplyMove(state, { type: 'Draw' });
    expect(drawn.ok).toBe(true);
    expect(drawn.next.waste).toHaveLength(1);

    const recycle = klondikeTryApplyMove(state, { type: 'Recycle' });
    expect(recycle.ok).toBe(false);
    expect(recycle.next).toBe(state); // unchanged on failure
  });
});

describe('Spider conceptual API', () => {
  it('starts at 500 and a deal costs one point', () => {
    const state = spiderNewGame(1, { suitCount: 4 });
    expect(state.score).toBe(500);
    const dealt = spiderTryApplyMove(state, { type: 'Deal' });
    expect(dealt.ok).toBe(true);
    expect(dealt.next.score).toBe(499);
  });
});

describe('common interface', () => {
  it('resolves engines by variant id (case-insensitive)', () => {
    expect(solitaireEngines.for('klondike').variant).toBe('klondike');
    expect(solitaireEngines.for('SPIDER').variant).toBe('spider');
    expect(solitaireEngines.for('FREECELL').variant).toBe('freecell');
    expect(solitaireEngines.for('PYRAMID').variant).toBe('pyramid');
    expect(solitaireEngines.for('TRIPEAKS').variant).toBe('tripeaks');
    expect(() => solitaireEngines.for('canfield')).toThrow();
  });
});
