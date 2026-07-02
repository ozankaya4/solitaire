// Variant-agnostic wrapper over the typed engines so the board can drive any
// variant uniformly (create, legal moves, apply, win, score).

import {
  klondikeGetLegalMoves,
  klondikeIsWon,
  klondikeNewGame,
  klondikeOptionsFromBag,
  klondikeTryApplyMove,
  type KlondikeState,
} from '../engine/klondike';
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

export type AnyState = KlondikeState | SpiderState;

/** Variants that currently have a playable TypeScript engine + board. */
export function isPlayable(variant: VariantId): boolean {
  return variant === 'klondike' || variant === 'spider';
}

export function createGame(
  variant: VariantId,
  seed: number,
  bag: Readonly<Record<string, number>>,
): AnyState {
  if (variant === 'spider') {
    return spiderNewGame(seed, spiderOptionsFromBag(bag));
  }
  return klondikeNewGame(seed, klondikeOptionsFromBag(bag));
}

export function legalMoves(variant: VariantId, state: AnyState): MoveDto[] {
  return variant === 'spider'
    ? spiderGetLegalMoves(state as SpiderState)
    : klondikeGetLegalMoves(state as KlondikeState);
}

export interface ApplyResult {
  readonly ok: boolean;
  readonly next: AnyState;
  readonly scoreDelta: number;
}

export function applyMove(variant: VariantId, state: AnyState, move: MoveDto): ApplyResult {
  return variant === 'spider'
    ? spiderTryApplyMove(state as SpiderState, move)
    : klondikeTryApplyMove(state as KlondikeState, move);
}

export function isWon(variant: VariantId, state: AnyState): boolean {
  return variant === 'spider'
    ? spiderIsWon(state as SpiderState)
    : klondikeIsWon(state as KlondikeState);
}

export function scoreOf(state: AnyState): number {
  return state.score;
}
