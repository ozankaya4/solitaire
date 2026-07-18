import { describe, expect, it } from 'vitest';
import {
  klondikeGetLegalMoves,
  klondikeNewGame,
  klondikeTryApplyMove,
  KLONDIKE_UNLIMITED_REDEALS,
  type KlondikeState,
} from '../../engine/klondike';
import { spiderNewGame, spiderTryApplyMove } from '../../engine/spider';
import { emptyPile } from '../../engine/tableau';
import { getHint, getKlondikeHint, getSpiderHint } from '../hints';
import {
  createSpiderEndlessProvider,
  deriveSeed,
  getLevel,
  getLevelProvider,
  isServableKlondikeSeed,
  KlondikeCuratedProvider,
  levelCounterHintBudget,
  spiderHintBudget,
} from '../levels';
import {
  advanceLevel,
  createLocalStorageProgressStore,
  createMemoryProgressStore,
  getCurrentLevel,
  setCurrentLevel,
} from '../progress';
import { hintsRemaining, startLevelSession, useHint } from '../session';

const KLONDIKE_OPTIONS = { drawCount: 1, maxRedeals: KLONDIKE_UNLIMITED_REDEALS };

describe('hints surface a legal move', () => {
  it('Klondike: returns a move that the engine accepts', () => {
    const state = klondikeNewGame(1, KLONDIKE_OPTIONS);
    const hint = getKlondikeHint(state);
    expect(hint).not.toBeNull();
    expect(klondikeTryApplyMove(state, hint!).ok).toBe(true);
  });

  it('Spider: suggests a legal move on a fresh game', () => {
    const state = spiderNewGame(1, { suitCount: 4 });
    const hint = getSpiderHint(state);
    expect(hint).not.toBeNull();
    expect(spiderTryApplyMove(state, hint!).ok).toBe(true);
  });

  it('returns null when there are no legal moves', () => {
    const stuck: KlondikeState = {
      options: KLONDIKE_OPTIONS,
      stock: [],
      waste: [],
      foundations: [0, 0, 0, 0],
      tableau: [emptyPile, emptyPile, emptyPile, emptyPile, emptyPile, emptyPile, emptyPile],
      score: 0,
      redealsUsed: 0,
    };
    expect(klondikeGetLegalMoves(stuck)).toHaveLength(0);
    expect(getKlondikeHint(stuck)).toBeNull();
  });

  it('dispatches by variant', () => {
    expect(getHint('klondike', klondikeNewGame(2, KLONDIKE_OPTIONS))).not.toBeNull();
    expect(getHint('spider', spiderNewGame(2, { suitCount: 2 }))).not.toBeNull();
  });
});

describe('hint budget in the level session', () => {
  it('starts with the level budget and decrements per hint', () => {
    const def = getLevel('klondike', 1); // easy => 5 hints
    let session = startLevelSession(def);
    expect(hintsRemaining(session)).toBe(5);

    const first = useHint(session);
    expect(first.used).toBe(true);
    expect(hintsRemaining(first.session)).toBe(4);
    session = first.session;
  });

  it('clamps at zero and refuses further hints', () => {
    const def = getLevel('klondike', 41); // hard => 1 hint
    let session = startLevelSession(def);
    expect(hintsRemaining(session)).toBe(1);

    const first = useHint(session);
    expect(first.used).toBe(true);
    session = first.session;
    expect(hintsRemaining(session)).toBe(0);

    const second = useHint(session);
    expect(second.used).toBe(false);
    expect(hintsRemaining(second.session)).toBe(0);
  });
});

describe('endless difficulty proxies', () => {
  it('Spider hint budget scales inversely with suit count', () => {
    expect(spiderHintBudget(1)).toBe(5);
    expect(spiderHintBudget(2)).toBe(3);
    expect(spiderHintBudget(4)).toBe(1);
  });

  it('level-counter budget shrinks as levels rise', () => {
    expect(levelCounterHintBudget(1)).toBe(5);
    expect(levelCounterHintBudget(11)).toBe(4);
    expect(levelCounterHintBudget(100)).toBe(1);
    expect(levelCounterHintBudget(1)).toBeGreaterThan(levelCounterHintBudget(100));
  });

  it('Spider provider sets options and a suit-scaled budget', () => {
    const easy = createSpiderEndlessProvider(1).getLevel(3);
    expect(easy.options['suitCount']).toBe(1);
    expect(easy.hintBudget).toBe(5);
    expect(easy.source).toBe('generated');

    const hard = createSpiderEndlessProvider(4).getLevel(3);
    expect(hard.hintBudget).toBe(1);
  });
});

describe('level providers', () => {
  it('serves curated Klondike levels from the library', () => {
    const def = getLevel('klondike', 1);
    expect(def.source).toBe('curated');
    expect(def.grade).toBe('easy');
    expect(def.metrics).toBeDefined();
  });

  it(
    'generates solver-checked Klondike levels beyond the curated range',
    { timeout: 20000 },
    () => {
      // A smaller solver budget keeps this runtime test fast; a returned deal is
      // still guaranteed solvable within that budget.
      const budget = 60_000;
      const provider = new KlondikeCuratedProvider(undefined, budget);
      const beyond = provider.curatedCount + 1;
      const def = provider.getLevel(beyond);
      expect(def.source).toBe('generated');
      expect(def.grade).toBe('hard');
      // The served deal is guaranteed solvable.
      expect(isServableKlondikeSeed(def.seed, def.options['drawCount'] ?? 1, budget)).toBe(true);
      // Deterministic: same level number yields the same deal.
      expect(provider.getLevel(beyond).seed).toBe(def.seed);
    },
  );

  it('derives stable seeds per (variant, level, attempt)', () => {
    expect(deriveSeed('klondike', 50, 0)).toBe(deriveSeed('klondike', 50, 0));
    expect(deriveSeed('klondike', 50, 0)).not.toBe(deriveSeed('klondike', 50, 1));
  });

  it('throws for an unregistered variant', () => {
    expect(() => getLevelProvider('canfield')).toThrow();
  });
});

describe('progress persistence', () => {
  it('defaults to level 1 and round-trips through memory', () => {
    const store = createMemoryProgressStore();
    expect(getCurrentLevel(store, 'klondike')).toBe(1);
    setCurrentLevel(store, 'klondike', 7);
    expect(getCurrentLevel(store, 'klondike')).toBe(7);
    expect(advanceLevel(store, 'klondike')).toBe(8);
    // Per-variant isolation.
    expect(getCurrentLevel(store, 'spider')).toBe(1);
  });

  it('persists through a Web Storage implementation', () => {
    const store = createLocalStorageProgressStore(fakeStorage());
    expect(getCurrentLevel(store, 'spider')).toBe(1);
    setCurrentLevel(store, 'spider', 12);
    expect(getCurrentLevel(store, 'spider')).toBe(12);
  });
});

function fakeStorage(): Storage {
  const map = new Map<string, string>();
  return {
    get length() {
      return map.size;
    },
    clear: () => map.clear(),
    getItem: (key) => map.get(key) ?? null,
    key: (index) => [...map.keys()][index] ?? null,
    removeItem: (key) => {
      map.delete(key);
    },
    setItem: (key, value) => {
      map.set(key, value);
    },
  };
}
