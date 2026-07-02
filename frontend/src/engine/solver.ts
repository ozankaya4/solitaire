// Bounded depth-first Klondike solver.
//
// LIMITS (documented): this is NOT a complete solver. It explores states with a
// heuristic move ordering and a visited-state cache, capped by a node budget.
//  - A returned solution is always valid (verified move-by-move as it is built),
//    so `solved: true` guarantees the deal is solvable.
//  - `solved: false` with `exhausted: true` means the budget ran out before a
//    solution was found — the deal *may* still be solvable (false negative).
//  - `solved: false` with `exhausted: false` means the reachable state space was
//    fully searched with no win — the deal is genuinely unsolvable under the
//    solver's move set (it omits Foundation→Tableau moves to bound branching).
//  - Draw-1 deals are far more tractable than draw-3.
// It is used offline to grade/curate deals and at runtime to confirm a randomly
// generated deal is solvable before it is served.

import { faceUpCount } from './tableau';
import {
  klondikeGetLegalMoves,
  klondikeIsWon,
  klondikeNewGame,
  klondikeTryApplyMove,
  type KlondikeOptions,
  type KlondikeState,
} from './klondike';
import type { MoveDto } from './types';

export const DEFAULT_SOLVER_BUDGET = 200_000;

export interface SolveMetrics {
  /** States expanded (a proxy for how hard the deal was to search). */
  readonly nodesExpanded: number;
  /** Number of moves in the found solution (0 if unsolved). */
  readonly solutionLength: number;
  /** Largest legal-move count seen at any expanded state. */
  readonly maxBranching: number;
}

export interface SolveResult {
  readonly solved: boolean;
  /** True if the node budget was hit before finishing (result is inconclusive). */
  readonly exhausted: boolean;
  readonly moves: readonly MoveDto[] | null;
  readonly metrics: SolveMetrics;
}

interface Frame {
  readonly state: KlondikeState;
  readonly moves: readonly MoveDto[];
  index: number;
  readonly move?: MoveDto; // the move that produced this frame's state
}

/** Attempts to solve a Klondike deal within a node budget. */
export function solveKlondike(
  seed: number,
  options: KlondikeOptions,
  budget: number = DEFAULT_SOLVER_BUDGET,
): SolveResult {
  const start = klondikeNewGame(seed, options);
  let nodesExpanded = 0;
  let maxBranching = 0;

  const orderedFor = (state: KlondikeState): readonly MoveDto[] => {
    const moves = orderedMoves(state);
    if (moves.length > maxBranching) {
      maxBranching = moves.length;
    }
    return moves;
  };

  if (klondikeIsWon(start)) {
    return { solved: true, exhausted: false, moves: [], metrics: metrics(0, 0, 0) };
  }

  const visited = new Set<string>([keyOf(start)]);
  const stack: Frame[] = [{ state: start, moves: orderedFor(start), index: 0 }];

  while (stack.length > 0) {
    if (nodesExpanded > budget) {
      return {
        solved: false,
        exhausted: true,
        moves: null,
        metrics: metrics(nodesExpanded, 0, maxBranching),
      };
    }

    const frame = stack[stack.length - 1]!;
    if (frame.index >= frame.moves.length) {
      stack.pop();
      continue;
    }

    const move = frame.moves[frame.index++]!;
    const applied = klondikeTryApplyMove(frame.state, move);
    if (!applied.ok) {
      continue;
    }

    const key = keyOf(applied.next);
    if (visited.has(key)) {
      continue;
    }
    visited.add(key);
    nodesExpanded++;

    if (klondikeIsWon(applied.next)) {
      const solution = stack.slice(1).map((f) => f.move!);
      solution.push(move);
      return {
        solved: true,
        exhausted: false,
        moves: solution,
        metrics: metrics(nodesExpanded, solution.length, maxBranching),
      };
    }

    stack.push({ state: applied.next, moves: orderedFor(applied.next), index: 0, move });
  }

  return {
    solved: false,
    exhausted: false,
    moves: null,
    metrics: metrics(nodesExpanded, 0, maxBranching),
  };
}

/** Convenience gate: is this deal solvable within the budget? */
export function isKlondikeSolvable(
  seed: number,
  options: KlondikeOptions,
  budget: number = DEFAULT_SOLVER_BUDGET,
): boolean {
  return solveKlondike(seed, options, budget).solved;
}

function metrics(
  nodesExpanded: number,
  solutionLength: number,
  maxBranching: number,
): SolveMetrics {
  return { nodesExpanded, solutionLength, maxBranching };
}

// Heuristic move ordering (best first). Foundation→Tableau is omitted to keep the
// branching factor bounded; most deals are solvable without it.
function orderedMoves(state: KlondikeState): MoveDto[] {
  return klondikeGetLegalMoves(state)
    .filter((m) => m.type !== 'FoundationToTableau')
    .sort((a, b) => priority(state, b) - priority(state, a));
}

function priority(state: KlondikeState, move: MoveDto): number {
  switch (move.type) {
    case 'TableauToFoundation':
      return 100;
    case 'WasteToFoundation':
      return 95;
    case 'TableauToTableau':
      return flipsCard(state, move) ? 80 : 40;
    case 'WasteToTableau':
      return 60;
    case 'Draw':
      return 20;
    case 'Recycle':
      return 10;
    default:
      return 0;
  }
}

function flipsCard(state: KlondikeState, move: MoveDto): boolean {
  const source = state.tableau[move.source ?? -1];
  if (source === undefined) {
    return false;
  }
  return source.faceDownCount > 0 && (move.count ?? 0) === faceUpCount(source);
}

// Canonical key for visited-state pruning. Redeal count is excluded so that
// returning to a previously seen board (e.g. after a full stock cycle) is pruned,
// preventing infinite draw/recycle loops.
function keyOf(state: KlondikeState): string {
  const parts: string[] = [];
  parts.push(state.stock.map(ordinal).join(','));
  parts.push(state.waste.map(ordinal).join(','));
  parts.push(state.foundations.join(','));
  for (const pile of state.tableau) {
    parts.push(`${pile.faceDownCount}:${pile.cards.map(ordinal).join(',')}`);
  }
  return parts.join('|');
}

function ordinal(card: { suit: number; rank: number }): number {
  return card.suit * 13 + (card.rank - 1);
}
