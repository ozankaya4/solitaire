// The board's game session: engine state history (unlimited undo), hint budget,
// selection for tap-to-move, win handling, level integration, and the
// save/resume session rules.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { Card } from '../engine/cards';
import { KLONDIKE_UNLIMITED_REDEALS } from '../engine/klondike';
import type { MoveDto } from '../engine/types';
import type { Grade } from '../game/grading';
import { getHint } from '../game/hints';
import { getLevel } from '../game/levels';
import { advanceLevel, getCurrentLevel } from '../game/progress';
import { recordGameStarted, recordWin } from '../storage/cache';
import { createCacheProgressStore } from '../storage/progressStore';
import { useSettings } from '../app/settings';
import type { VariantId } from '../app/types';
import { boardModel, UNASSIGNED_SLOTS, type BoardModel, type FoundationSlots } from './boardModel';
import type { KlondikeState } from '../engine/klondike';
import { applyMove, createGame, isPlayable, isWon, scoreOf, type AnyState } from './engineAdapter';
import { autoMove, autoToFoundation, findPile, moveBetween, stockMove } from './moves';
import { clearSavedGame, loadSavedGame, saveGame } from './persistence';

export interface Selection {
  readonly pileId: string;
  readonly index: number;
}

export interface HintHighlight {
  readonly sourceId: string;
  readonly destId?: string;
}

interface GameData {
  readonly variant: VariantId;
  readonly level: number;
  readonly grade: Grade;
  readonly seed: number;
  readonly bag: Record<string, number>;
  readonly states: readonly AnyState[];
  readonly moves: readonly MoveDto[];
  readonly hintBudget: number;
  readonly hintsUsed: number;
  /** Play time carried over from prior sessions of this game (0 for a fresh deal). */
  readonly elapsedMs: number;
}

/** A finished, won game — everything needed to submit it for verification. */
export interface GameResult {
  readonly variant: VariantId;
  readonly level: number;
  readonly seed: number;
  readonly options: Record<string, number>;
  readonly moves: readonly MoveDto[];
  readonly score: number;
  readonly timeMs: number;
}

export interface Game {
  readonly variant: VariantId;
  readonly supported: boolean;
  readonly model: BoardModel | null;
  readonly level: number;
  readonly grade: Grade;
  readonly score: number;
  readonly won: boolean;
  /** Set once when the current game is won; cleared on a new deal. */
  readonly lastWin: GameResult | null;
  readonly canUndo: boolean;
  readonly hintsRemaining: number;
  readonly hintBudget: number;
  readonly selected: Selection | null;
  readonly hint: HintHighlight | null;
  readonly dealNonce: number;
  onCardTap: (pileId: string, index: number) => void;
  onCardDoubleTap: (pileId: string, index: number) => void;
  onPileTap: (pileId: string) => void;
  onStock: () => void;
  onDrop: (srcId: string, srcIndex: number, destId: string) => boolean;
  undo: () => void;
  requestHint: () => void;
  newDeal: () => void;
  switchVariant: (variant: VariantId) => void;
}

const progress = createCacheProgressStore();

function bagFor(
  variant: VariantId,
  drawMode: number,
  levelBag: Readonly<Record<string, number>>,
): Record<string, number> {
  // Klondike honors the player's draw setting; the level supplies the seed.
  if (variant === 'klondike') {
    return { drawCount: drawMode, maxRedeals: KLONDIKE_UNLIMITED_REDEALS };
  }
  return { ...levelBag };
}

function startLevel(variant: VariantId, drawMode: number): GameData {
  const level = getCurrentLevel(progress, variant);
  const def = getLevel(variant, level);
  const bag = bagFor(variant, drawMode, def.options);
  return {
    variant,
    level,
    grade: def.grade,
    seed: def.seed,
    bag,
    states: [createGame(variant, def.seed, bag)],
    moves: [],
    hintBudget: def.hintBudget,
    hintsUsed: 0,
    elapsedMs: 0,
  };
}

function resume(variant: VariantId): GameData | null {
  const saved = loadSavedGame(variant);
  if (!saved) {
    return null;
  }
  const states: AnyState[] = [createGame(variant, saved.seed, saved.bag)];
  const moves: MoveDto[] = [];
  for (const move of saved.moves) {
    const result = applyMove(variant, states[states.length - 1]!, move);
    if (!result.ok) {
      break; // corrupt save — stop replaying, keep what is valid
    }
    states.push(result.next);
    moves.push(move);
  }
  const def = getLevel(variant, saved.level);
  return {
    variant,
    level: saved.level,
    grade: def.grade,
    seed: saved.seed,
    bag: saved.bag,
    states,
    moves,
    hintBudget: def.hintBudget,
    hintsUsed: saved.hintsUsed,
    elapsedMs: saved.elapsedMs ?? 0,
  };
}

function begin(variant: VariantId, drawMode: number): GameData {
  if (!isPlayable(variant)) {
    return {
      variant,
      level: 1,
      grade: 'easy',
      seed: 0,
      bag: {},
      states: [],
      moves: [],
      hintBudget: 0,
      hintsUsed: 0,
      elapsedMs: 0,
    };
  }
  return resume(variant) ?? startLevel(variant, drawMode);
}

export function useGame(initialVariant?: VariantId): Game {
  const { defaultVariant, drawMode } = useSettings();
  const drawRef = useRef(drawMode);
  drawRef.current = drawMode;

  // Board entry uses the settings default variant, unless an explicit variant is
  // requested (e.g. resuming a specific game from the Saved-games list).
  const startVariant = initialVariant ?? defaultVariant;
  const [data, setData] = useState<GameData>(() => begin(startVariant, drawMode));
  const [selected, setSelected] = useState<Selection | null>(null);
  const [hint, setHint] = useState<HintHighlight | null>(null);
  const [dealNonce, setDealNonce] = useState(0);
  // Klondike: which suit occupies which visual foundation slot (aces claim slots).
  const [foundationSlots, setFoundationSlots] = useState<FoundationSlots>(UNASSIGNED_SLOTS);
  const [lastWin, setLastWin] = useState<GameResult | null>(null);
  const wonHandled = useRef(false);
  // Stats: a "fresh" deal (no resume) counts as a game played; a resume does not.
  const fresh = useRef(isPlayable(startVariant) && loadSavedGame(startVariant) === null);
  const startedAt = useRef(Date.now());

  const supported = isPlayable(data.variant) && data.states.length > 0;
  const current = data.states[data.states.length - 1];
  const model = useMemo<BoardModel | null>(
    () => (supported && current ? boardModel(data.variant, current, foundationSlots) : null),
    [supported, current, data.variant, foundationSlots],
  );
  const won = supported && current ? isWon(data.variant, current) : false;

  // Keep the slot mapping consistent with the engine state: release slots whose
  // foundation emptied (undo), and give any unassigned non-empty suit the first
  // free slot (auto-moves, hints, double-tap, resumed games).
  useEffect(() => {
    if (data.variant !== 'klondike' || !current) {
      return;
    }
    const foundations = (current as KlondikeState).foundations;
    setFoundationSlots((prev) => {
      let next: (number | null)[] | null = null;
      prev.forEach((suit, slot) => {
        if (suit !== null && (foundations[suit] ?? 0) === 0) {
          next ??= [...prev];
          next[slot] = null;
        }
      });
      for (let suit = 0; suit < 4; suit++) {
        const assigned = (next ?? prev).includes(suit);
        if ((foundations[suit] ?? 0) > 0 && !assigned) {
          next ??= [...prev];
          const free = next.indexOf(null);
          if (free !== -1) {
            next[free] = suit;
          }
        }
      }
      return next ?? prev;
    });
  }, [current, data.variant]);

  // When a move explicitly targets an empty foundation slot, that slot adopts
  // the moved ace's suit (before the effect above would pick the first free one).
  const claimSlot = useCallback(
    (destId: string, srcId: string, srcIndex: number) => {
      if (data.variant !== 'klondike' || !destId.startsWith('fslot-') || !model) {
        return;
      }
      const slot = Number(destId.slice('fslot-'.length));
      const suit = findPile(model, srcId)?.cards[srcIndex]?.card.suit;
      if (suit === undefined || !Number.isInteger(slot)) {
        return;
      }
      setFoundationSlots((prev) =>
        prev[slot] !== null || prev.includes(suit)
          ? prev
          : prev.map((s, i) => (i === slot ? suit : s)),
      );
    },
    [data.variant, model],
  );

  // Session rules: persist unfinished games; a won game clears its save and
  // advances the level (once).
  useEffect(() => {
    if (!supported) {
      return;
    }
    if (won) {
      if (!wonHandled.current) {
        wonHandled.current = true;
        // Total play time = time carried over from prior sessions + this session.
        const elapsed = data.elapsedMs + (Date.now() - startedAt.current);
        advanceLevel(progress, data.variant);
        clearSavedGame(data.variant);
        recordWin(data.variant, elapsed);
        // Expose the finished game so an authenticated screen can submit it to
        // the leaderboard (guests keep this entirely local).
        setLastWin({
          variant: data.variant,
          level: data.level,
          seed: data.seed,
          options: data.bag,
          moves: [...data.moves],
          score: current ? scoreOf(current) : 0,
          timeMs: elapsed,
        });
      }
      return;
    }
    wonHandled.current = false;
    saveGame({
      variant: data.variant,
      level: data.level,
      seed: data.seed,
      bag: data.bag,
      moves: [...data.moves],
      hintsUsed: data.hintsUsed,
      elapsedMs: data.elapsedMs + (Date.now() - startedAt.current),
    });
  }, [data, supported, won, current]);

  // Count a fresh deal as a game played (a resume does not count).
  useEffect(() => {
    startedAt.current = Date.now();
    if (fresh.current && isPlayable(data.variant)) {
      recordGameStarted(data.variant);
    }
  }, [dealNonce, data.variant]);

  const commit = useCallback((move: MoveDto) => {
    setSelected(null);
    setHint(null);
    setData((d) => {
      const cur = d.states[d.states.length - 1];
      if (!cur) {
        return d;
      }
      const result = applyMove(d.variant, cur, move);
      if (!result.ok) {
        return d;
      }
      return { ...d, states: [...d.states, result.next], moves: [...d.moves, move] };
    });
  }, []);

  const isSelectable = useCallback(
    (pileId: string, index: number): boolean => {
      if (!model) {
        return false;
      }
      const pile = findPile(model, pileId);
      const card = pile?.cards[index];
      if (!pile || !card || !card.faceUp) {
        return false;
      }
      if (pile.kind === 'waste' || pile.kind === 'foundation') {
        return index === pile.cards.length - 1; // only the top card
      }
      return pile.kind === 'tableau';
    },
    [model],
  );

  // A lone ace may be moved between empty foundation slots. The engine keys
  // foundations by suit, so this is UI-only: no engine move, just remap which
  // slot displays that suit (freeing the old one).
  const reassignSlot = useCallback(
    (srcId: string, srcIndex: number, destId: string): boolean => {
      if (data.variant !== 'klondike' || !model || !destId.startsWith('fslot-')) {
        return false;
      }
      const src = findPile(model, srcId);
      const card = src?.cards[srcIndex];
      // Source must be a foundation holding only its ace (a built pile can't move).
      if (!src || src.kind !== 'foundation' || src.cards.length !== 1 || card?.card.rank !== 1) {
        return false;
      }
      const toSlot = Number(destId.slice('fslot-'.length));
      if (!Number.isInteger(toSlot)) {
        return false;
      }
      const suit = card.card.suit;
      setFoundationSlots((prev) =>
        prev.map((s, i) => (i === toSlot ? suit : s === suit ? null : s)),
      );
      setSelected(null);
      setHint(null);
      return true;
    },
    [data.variant, model],
  );

  // Resolve a source→dest gesture: try a UI-only slot reassignment first, then a
  // real engine move (claiming an empty foundation slot for the moved ace).
  const applyGesture = useCallback(
    (srcId: string, srcIndex: number, destId: string): boolean => {
      if (!model || !current) {
        return false;
      }
      if (reassignSlot(srcId, srcIndex, destId)) {
        return true;
      }
      const move = moveBetween(data.variant, current, model, srcId, srcIndex, destId);
      if (move) {
        claimSlot(destId, srcId, srcIndex);
        commit(move);
        return true;
      }
      return false;
    },
    [model, current, data.variant, reassignSlot, claimSlot, commit],
  );

  const onCardTap = useCallback(
    (pileId: string, index: number) => {
      if (!model || !current) {
        return;
      }
      if (selected) {
        if (selected.pileId === pileId && selected.index === index) {
          const auto = autoMove(data.variant, current, model, pileId, index);
          if (auto) {
            claimSlot(auto.destId, pileId, index);
            commit(auto.move);
          } else {
            setSelected(null);
          }
          return;
        }
        if (applyGesture(selected.pileId, selected.index, pileId)) {
          return;
        }
        setSelected(isSelectable(pileId, index) ? { pileId, index } : null);
        return;
      }
      if (isSelectable(pileId, index)) {
        setSelected({ pileId, index });
      }
    },
    [model, current, selected, data.variant, applyGesture, isSelectable, claimSlot, commit],
  );

  const onPileTap = useCallback(
    (pileId: string) => {
      if (!selected) {
        return;
      }
      if (!applyGesture(selected.pileId, selected.index, pileId)) {
        setSelected(null);
      }
    },
    [selected, applyGesture],
  );

  const onCardDoubleTap = useCallback(
    (pileId: string, index: number) => {
      if (!model || !current) {
        return;
      }
      const move = autoToFoundation(data.variant, current, model, pileId, index);
      if (move) {
        commit(move);
      }
    },
    [model, current, data.variant, commit],
  );

  const onStock = useCallback(() => {
    if (!current) {
      return;
    }
    const move = stockMove(data.variant, current);
    if (move) {
      commit(move);
    }
  }, [current, data.variant, commit]);

  const onDrop = useCallback(
    (srcId: string, srcIndex: number, destId: string): boolean => {
      if (applyGesture(srcId, srcIndex, destId)) {
        return true;
      }
      setSelected(null);
      return false;
    },
    [applyGesture],
  );

  const undo = useCallback(() => {
    setSelected(null);
    setHint(null);
    setData((d) =>
      d.states.length > 1
        ? { ...d, states: d.states.slice(0, -1), moves: d.moves.slice(0, -1) }
        : d,
    );
  }, []);

  const requestHint = useCallback(() => {
    if (!model || !current || data.hintsUsed >= data.hintBudget) {
      return;
    }
    const move = getHint(data.variant, current);
    if (!move) {
      return;
    }
    setHint(hintHighlight(current, move));
    setData((d) => ({ ...d, hintsUsed: d.hintsUsed + 1 }));
    window.setTimeout(() => setHint(null), 2600);
  }, [model, current, data.variant, data.hintsUsed, data.hintBudget]);

  const newDeal = useCallback(() => {
    wonHandled.current = false;
    fresh.current = true;
    setSelected(null);
    setHint(null);
    setLastWin(null);
    setData(startLevel(data.variant, drawRef.current));
    setDealNonce((n) => n + 1);
  }, [data.variant]);

  const switchVariant = useCallback((variant: VariantId) => {
    wonHandled.current = false;
    fresh.current = isPlayable(variant) && loadSavedGame(variant) === null;
    setSelected(null);
    setHint(null);
    setLastWin(null);
    setData(begin(variant, drawRef.current));
    setDealNonce((n) => n + 1);
  }, []);

  return {
    variant: data.variant,
    supported,
    model,
    level: data.level,
    grade: data.grade,
    score: current ? scoreOf(current) : 0,
    won,
    lastWin,
    canUndo: data.states.length > 1,
    hintsRemaining: Math.max(0, data.hintBudget - data.hintsUsed),
    hintBudget: data.hintBudget,
    selected,
    hint,
    dealNonce,
    onCardTap,
    onCardDoubleTap,
    onPileTap,
    onStock,
    onDrop,
    undo,
    requestHint,
    newDeal,
    switchVariant,
  };
}

// Maps a hinted move to the pile ids to highlight.
function hintHighlight(state: AnyState, move: MoveDto): HintHighlight {
  const topSuit = (cards: readonly Card[]): number => cards[cards.length - 1]?.suit ?? 0;
  switch (move.type) {
    case 'Draw':
    case 'Recycle':
    case 'Deal':
      return { sourceId: 'stock' };
    case 'WasteToFoundation': {
      const waste = (state as { waste: readonly Card[] }).waste;
      return { sourceId: 'waste', destId: `foundation-${topSuit(waste)}` };
    }
    case 'WasteToTableau':
      return { sourceId: 'waste', destId: `tableau-${move.destination ?? 0}` };
    case 'TableauToFoundation': {
      const pile = (state as { tableau: readonly { cards: readonly Card[] }[] }).tableau[
        move.source ?? 0
      ];
      return {
        sourceId: `tableau-${move.source ?? 0}`,
        destId: `foundation-${topSuit(pile?.cards ?? [])}`,
      };
    }
    case 'TableauToTableau':
      return {
        sourceId: `tableau-${move.source ?? 0}`,
        destId: `tableau-${move.destination ?? 0}`,
      };
    case 'FoundationToTableau':
      return {
        sourceId: `foundation-${move.source ?? 0}`,
        destId: `tableau-${move.destination ?? 0}`,
      };
    default:
      return { sourceId: 'stock' };
  }
}
