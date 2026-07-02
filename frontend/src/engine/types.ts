// Variant-neutral types shared by every engine — the TypeScript mirror of
// MoveDto.cs, GameDefinition.cs, ReplayOutcome.cs and ISolitaireEngine.cs.

/** Portable, serialization-friendly move shape common to all variants. */
export interface MoveDto {
  readonly type: string;
  readonly source?: number;
  readonly destination?: number;
  readonly count?: number;
}

/** A fully portable description of a game to (re)play. */
export interface GameDefinition {
  readonly seed: number;
  readonly options: Readonly<Record<string, number>>;
  readonly moves: readonly MoveDto[];
}

/** The variant-neutral result of a replay. */
export interface ReplayOutcome {
  readonly score: number;
  readonly won: boolean;
  readonly allMovesLegal: boolean;
  readonly firstIllegalMoveIndex: number | null;
}

/** Strongly-typed replay result carrying the concrete final state. */
export interface ReplayResult<TState> extends ReplayOutcome {
  readonly finalState: TState;
}

/** Result of applying a single move (no C#-style out params in TS). */
export interface ApplyResult<TState> {
  readonly ok: boolean;
  readonly next: TState; // unchanged input state on failure
  readonly scoreDelta: number;
}

/** The common contract every variant exposes for uniform replay/verification. */
export interface SolitaireEngine {
  readonly variant: string;
  replay(game: GameDefinition): ReplayOutcome;
}
