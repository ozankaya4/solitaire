// A lightweight per-level session that tracks the remaining hint budget as part
// of game state. The engine states (KlondikeState/SpiderState) hold the cards;
// this holds the level-meta the engine does not know about.

import type { LevelDefinition } from './levels';

export interface LevelSession {
  readonly variant: string;
  readonly level: number;
  readonly hintBudget: number;
  readonly hintsUsed: number;
}

export function startLevelSession(def: LevelDefinition): LevelSession {
  return { variant: def.variant, level: def.level, hintBudget: def.hintBudget, hintsUsed: 0 };
}

export function hintsRemaining(session: LevelSession): number {
  return Math.max(0, session.hintBudget - session.hintsUsed);
}

export interface UseHintResult {
  readonly session: LevelSession;
  /** True if a hint was available and consumed; false if the budget was empty. */
  readonly used: boolean;
}

/** Consumes one hint if any remain; otherwise returns the session unchanged. */
export function useHint(session: LevelSession): UseHintResult {
  if (hintsRemaining(session) <= 0) {
    return { session, used: false };
  }
  return { session: { ...session, hintsUsed: session.hintsUsed + 1 }, used: true };
}
