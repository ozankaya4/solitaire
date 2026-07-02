// Hints: call the engine's legal-move generator and surface one good move.
// Move ranking is a lightweight heuristic (progress-first); the actual legality
// always comes from the engine, so a hint is always a playable move.

import { klondikeGetLegalMoves, type KlondikeState } from '../engine/klondike';
import { spiderGetLegalMoves, type SpiderState } from '../engine/spider';
import { faceUpCount } from '../engine/tableau';
import type { MoveDto } from '../engine/types';

/** Returns one good legal Klondike move, or null if none exist. */
export function getKlondikeHint(state: KlondikeState): MoveDto | null {
  return best(klondikeGetLegalMoves(state), (m) => klondikePriority(state, m));
}

/** Returns one good legal Spider move, or null if none exist. */
export function getSpiderHint(state: SpiderState): MoveDto | null {
  return best(spiderGetLegalMoves(state), (m) => spiderPriority(state, m));
}

/** Variant-dispatching hint used by the game layer. */
export function getHint(variant: string, state: KlondikeState | SpiderState): MoveDto | null {
  switch (variant) {
    case 'klondike':
      return getKlondikeHint(state as KlondikeState);
    case 'spider':
      return getSpiderHint(state as SpiderState);
    default:
      return null;
  }
}

function best(moves: readonly MoveDto[], priority: (m: MoveDto) => number): MoveDto | null {
  if (moves.length === 0) {
    return null;
  }
  let bestMove = moves[0]!;
  let bestScore = priority(bestMove);
  for (let i = 1; i < moves.length; i++) {
    const move = moves[i]!;
    const score = priority(move);
    if (score > bestScore) {
      bestScore = score;
      bestMove = move;
    }
  }
  return bestMove;
}

function klondikePriority(state: KlondikeState, move: MoveDto): number {
  switch (move.type) {
    case 'TableauToFoundation':
      return 100;
    case 'WasteToFoundation':
      return 95;
    case 'TableauToTableau':
      return klondikeFlips(state, move) ? 80 : 40;
    case 'WasteToTableau':
      return 60;
    case 'Draw':
      return 20;
    case 'Recycle':
      return 10;
    case 'FoundationToTableau':
      return 5;
    default:
      return 0;
  }
}

function klondikeFlips(state: KlondikeState, move: MoveDto): boolean {
  const source = state.tableau[move.source ?? -1];
  return (
    source !== undefined && source.faceDownCount > 0 && (move.count ?? 0) === faceUpCount(source)
  );
}

function spiderPriority(state: SpiderState, move: MoveDto): number {
  if (move.type !== 'TableauToTableau') {
    return 10; // Deal
  }
  const source = state.tableau[move.source ?? -1];
  const dest = state.tableau[move.destination ?? -1];
  if (source === undefined || dest === undefined) {
    return 0;
  }
  const bottom = source.cards[source.cards.length - (move.count ?? 0)];
  const destTop = dest.cards[dest.cards.length - 1];
  if (destTop !== undefined && bottom !== undefined && destTop.suit === bottom.suit) {
    return 100; // in-suit build — progresses toward a completed sequence
  }
  if (source.faceDownCount > 0 && (move.count ?? 0) === faceUpCount(source)) {
    return 80; // exposes a hidden card
  }
  return dest.cards.length === 0 ? 30 : 50;
}
