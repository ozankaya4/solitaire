// Translates board gestures (drag a run, tap-to-move, double-tap, stock click)
// into engine MoveDtos, choosing the first candidate the engine accepts.

import type { MoveDto } from '../engine/types';
import type { VariantId } from '../app/types';
import { applyMove, type AnyState } from './engineAdapter';
import type { BoardModel, Pile } from './boardModel';

export interface ParsedPile {
  readonly kind: string;
  readonly index: number;
}

export function parsePile(id: string): ParsedPile {
  const dash = id.indexOf('-');
  if (dash === -1) {
    return { kind: id, index: 0 };
  }
  return { kind: id.slice(0, dash), index: Number(id.slice(dash + 1)) };
}

export function findPile(model: BoardModel, id: string): Pile | undefined {
  if (model.stock?.id === id) {
    return model.stock;
  }
  if (model.waste?.id === id) {
    return model.waste;
  }
  return model.foundations.find((p) => p.id === id) ?? model.tableau.find((p) => p.id === id);
}

const legal = (variant: VariantId, state: AnyState, move: MoveDto): MoveDto | null =>
  applyMove(variant, state, move).ok ? move : null;

/** The move (if any) for taking cards from `srcIndex..end` of `srcId` onto `destId`. */
export function moveBetween(
  variant: VariantId,
  state: AnyState,
  model: BoardModel,
  srcId: string,
  srcIndex: number,
  destId: string,
): MoveDto | null {
  const src = findPile(model, srcId);
  const dest = parsePile(destId);
  if (!src || srcId === destId) {
    return null;
  }
  const count = src.cards.length - srcIndex;

  if (variant === 'spider') {
    if (src.kind === 'tableau' && dest.kind === 'tableau') {
      return legal(variant, state, {
        type: 'TableauToTableau',
        source: src.index,
        destination: dest.index,
        count,
      });
    }
    return null;
  }

  // Klondike
  const moving = src.cards[srcIndex]?.card;
  if (!moving) {
    return null;
  }
  // An assigned foundation slot only takes its own suit; an unassigned slot
  // ("fslot") takes any ace and will adopt that suit. The engine keys
  // foundations by suit, so both map to the same suit-implicit move — the
  // guards here keep the drop target honest with what the player sees.
  if (dest.kind === 'foundation' || dest.kind === 'fslot') {
    if (dest.kind === 'foundation' && Number(moving.suit) !== dest.index) {
      return null;
    }
    if (dest.kind === 'fslot' && moving.rank !== 1) {
      return null;
    }
    if (src.kind === 'waste') {
      return legal(variant, state, { type: 'WasteToFoundation' });
    }
    if (src.kind === 'tableau' && count === 1) {
      return legal(variant, state, { type: 'TableauToFoundation', source: src.index });
    }
    return null;
  }
  if (dest.kind === 'tableau') {
    if (src.kind === 'waste') {
      return legal(variant, state, { type: 'WasteToTableau', destination: dest.index });
    }
    if (src.kind === 'tableau') {
      return legal(variant, state, {
        type: 'TableauToTableau',
        source: src.index,
        destination: dest.index,
        count,
      });
    }
    if (src.kind === 'foundation') {
      return legal(variant, state, {
        type: 'FoundationToTableau',
        source: src.index,
        destination: dest.index,
      });
    }
  }
  return null;
}

export interface AutoMove {
  readonly move: MoveDto;
  readonly destId: string;
}

/** Best automatic destination for a tap-to-move: foundations first, then tableaux. */
export function autoMove(
  variant: VariantId,
  state: AnyState,
  model: BoardModel,
  srcId: string,
  srcIndex: number,
): AutoMove | null {
  const dests: string[] =
    variant === 'spider'
      ? model.tableau.map((p) => p.id)
      : [...model.foundations.map((p) => p.id), ...model.tableau.map((p) => p.id)];

  for (const destId of dests) {
    const move = moveBetween(variant, state, model, srcId, srcIndex, destId);
    if (move) {
      return { move, destId };
    }
  }
  return null;
}

/** Double-tap: send the card straight to its foundation (Klondike only). */
export function autoToFoundation(
  variant: VariantId,
  state: AnyState,
  model: BoardModel,
  srcId: string,
  srcIndex: number,
): MoveDto | null {
  if (variant !== 'klondike') {
    return autoMove(variant, state, model, srcId, srcIndex)?.move ?? null;
  }
  const src = parsePile(srcId);
  if (src.kind === 'waste') {
    return legal(variant, state, { type: 'WasteToFoundation' });
  }
  if (src.kind === 'tableau') {
    return legal(variant, state, { type: 'TableauToFoundation', source: src.index });
  }
  return null;
}

/** Clicking the stock: draw / deal / recycle. */
export function stockMove(variant: VariantId, state: AnyState): MoveDto | null {
  if (variant === 'spider') {
    return legal(variant, state, { type: 'Deal' });
  }
  return legal(variant, state, { type: 'Draw' }) ?? legal(variant, state, { type: 'Recycle' });
}
