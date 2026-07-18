// Hints: call the engine's legal-move generator and surface one good move.
// Move ranking is a lightweight heuristic (progress-first); the actual legality
// always comes from the engine, so a hint is always a playable move.

import { freecellGetLegalMoves, type FreeCellState } from '../engine/freecell';
import { klondikeGetLegalMoves, type KlondikeState } from '../engine/klondike';
import { pyramidGetLegalMoves, type PyramidState } from '../engine/pyramid';
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

/** Returns one good legal FreeCell move, or null if none exist. */
export function getFreeCellHint(state: FreeCellState): MoveDto | null {
  return best(freecellGetLegalMoves(state), (m) => freecellPriority(m));
}

/** Returns one good legal Pyramid move, or null if none exist. */
export function getPyramidHint(state: PyramidState): MoveDto | null {
  return best(pyramidGetLegalMoves(state), (m) => pyramidPriority(m));
}

/** Variant-dispatching hint used by the game layer. */
export function getHint(
  variant: string,
  state: KlondikeState | SpiderState | FreeCellState | PyramidState,
): MoveDto | null {
  switch (variant) {
    case 'klondike':
      return getKlondikeHint(state as KlondikeState);
    case 'spider':
      return getSpiderHint(state as SpiderState);
    case 'freecell':
      return getFreeCellHint(state as FreeCellState);
    case 'pyramid':
      return getPyramidHint(state as PyramidState);
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

function freecellPriority(move: MoveDto): number {
  switch (move.type) {
    case 'TableauToFoundation':
    case 'FreeCellToFoundation':
      return 100;
    // Longer supermoves make more visible progress than shuffling one card.
    case 'TableauToTableau':
      return 60 + (move.count ?? 1);
    case 'FreeCellToTableau':
      return 50; // unloading a free cell frees a resource
    case 'TableauToFreeCell':
      return 20; // parking a card is a last resort, but still worth suggesting
    case 'FoundationToTableau':
      return 5;
    default:
      return 0;
  }
}

function pyramidPriority(move: MoveDto): number {
  switch (move.type) {
    case 'RemovePair':
      return 100;
    case 'RemoveSingle':
      return 90;
    case 'Draw':
      return 20;
    case 'Recycle':
      return 10;
    default:
      return 0;
  }
}
