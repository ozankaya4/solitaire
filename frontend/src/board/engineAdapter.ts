// Variant-agnostic wrapper over the typed engines so the board can drive any
// variant uniformly (create, legal moves, apply, win, score).

import {
  freecellGetLegalMoves,
  freecellIsWon,
  freecellNewGame,
  freecellTryApplyMove,
  type FreeCellState,
} from '../engine/freecell';
import {
  klondikeGetLegalMoves,
  klondikeIsWon,
  klondikeNewGame,
  klondikeOptionsFromBag,
  klondikeTryApplyMove,
  type KlondikeState,
} from '../engine/klondike';
import {
  pyramidGetLegalMoves,
  pyramidIsWon,
  pyramidNewGame,
  pyramidTryApplyMove,
  type PyramidState,
} from '../engine/pyramid';
import {
  spiderGetLegalMoves,
  spiderIsWon,
  spiderNewGame,
  spiderOptionsFromBag,
  spiderTryApplyMove,
  type SpiderState,
} from '../engine/spider';
import type { MoveDto } from '../engine/types';
import type { VariantId } from '../app/types';

export type AnyState = KlondikeState | SpiderState | FreeCellState | PyramidState;

const PLAYABLE_VARIANTS: readonly VariantId[] = ['klondike', 'spider', 'freecell', 'pyramid'];

/** Variants that currently have a playable TypeScript engine + board. */
export function isPlayable(variant: VariantId): boolean {
  return PLAYABLE_VARIANTS.includes(variant);
}

export function createGame(
  variant: VariantId,
  seed: number,
  bag: Readonly<Record<string, number>>,
): AnyState {
  switch (variant) {
    case 'spider':
      return spiderNewGame(seed, spiderOptionsFromBag(bag));
    case 'freecell':
      return freecellNewGame(seed);
    case 'pyramid':
      return pyramidNewGame(seed);
    default:
      return klondikeNewGame(seed, klondikeOptionsFromBag(bag));
  }
}

export function legalMoves(variant: VariantId, state: AnyState): MoveDto[] {
  switch (variant) {
    case 'spider':
      return spiderGetLegalMoves(state as SpiderState);
    case 'freecell':
      return freecellGetLegalMoves(state as FreeCellState);
    case 'pyramid':
      return pyramidGetLegalMoves(state as PyramidState);
    default:
      return klondikeGetLegalMoves(state as KlondikeState);
  }
}

export interface ApplyResult {
  readonly ok: boolean;
  readonly next: AnyState;
  readonly scoreDelta: number;
}

export function applyMove(variant: VariantId, state: AnyState, move: MoveDto): ApplyResult {
  switch (variant) {
    case 'spider':
      return spiderTryApplyMove(state as SpiderState, move);
    case 'freecell':
      return freecellTryApplyMove(state as FreeCellState, move);
    case 'pyramid':
      return pyramidTryApplyMove(state as PyramidState, move);
    default:
      return klondikeTryApplyMove(state as KlondikeState, move);
  }
}

export function isWon(variant: VariantId, state: AnyState): boolean {
  switch (variant) {
    case 'spider':
      return spiderIsWon(state as SpiderState);
    case 'freecell':
      return freecellIsWon(state as FreeCellState);
    case 'pyramid':
      return pyramidIsWon(state as PyramidState);
    default:
      return klondikeIsWon(state as KlondikeState);
  }
}

export function scoreOf(state: AnyState): number {
  return state.score;
}
