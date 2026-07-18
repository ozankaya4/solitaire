// Level representation and per-variant level providers.
//
// Klondike ships a curated, solver-graded ladder (imported JSON); levels beyond
// the curated range are generated deterministically and solver-checked before
// being served. The other variants ship as "endless" mode: deterministic random
// deals with a hint budget scaled by a lightweight difficulty proxy. The
// LevelProvider interface + registry let a curated ladder be added per variant
// later without touching callers.

import klondikeLibrary from '../levels/klondike.levels.json';
import { KLONDIKE_UNLIMITED_REDEALS } from '../engine/klondike';
import { DeterministicRandom } from '../engine/random';
import {
  DEFAULT_SOLVER_BUDGET,
  isKlondikeSolvable,
  solveKlondike,
  type SolveMetrics,
} from '../engine/solver';
import { gradeForLevel, hintBudgetForGrade, type Grade } from './grading';

export interface LevelDefinition {
  readonly variant: string;
  readonly level: number;
  readonly seed: number;
  readonly options: Readonly<Record<string, number>>;
  readonly grade: Grade;
  readonly hintBudget: number;
  readonly source: 'curated' | 'generated';
  readonly metrics?: SolveMetrics;
}

export interface LevelProvider {
  readonly variant: string;
  getLevel(level: number): LevelDefinition;
}

interface CuratedEntry {
  readonly level: number;
  readonly seed: number;
  readonly grade: Grade;
  readonly hintBudget: number;
  readonly metrics: SolveMetrics;
}

interface CuratedLibrary {
  readonly variant: string;
  readonly solverBudget: number;
  readonly options: Record<string, number>;
  readonly levels: readonly CuratedEntry[];
}

const KLONDIKE_LIBRARY = klondikeLibrary as unknown as CuratedLibrary;

/** Deterministic seed derivation so a given (variant, level, attempt) is stable. */
export function deriveSeed(variant: string, level: number, attempt: number): number {
  const base = (hashString(variant) ^ (level * 73856093) ^ (attempt * 19349663)) >>> 0;
  return new DeterministicRandom(base).nextUint32() | 0;
}

function hashString(value: string): number {
  let hash = 2166136261;
  for (let i = 0; i < value.length; i++) {
    hash = Math.imul(hash ^ value.charCodeAt(i), 16777619);
  }
  return hash >>> 0;
}

/** Klondike: curated ladder + solver-checked deterministic generation beyond it. */
export class KlondikeCuratedProvider implements LevelProvider {
  readonly variant = 'klondike';
  private readonly curated = new Map<number, CuratedEntry>();
  private readonly options: Readonly<Record<string, number>>;
  private readonly generationBudget: number;

  constructor(library: CuratedLibrary = KLONDIKE_LIBRARY, generationBudget?: number) {
    for (const entry of library.levels) {
      this.curated.set(entry.level, entry);
    }
    this.options = library.options;
    this.generationBudget = generationBudget ?? library.solverBudget;
  }

  /** Number of curated levels available. */
  get curatedCount(): number {
    return this.curated.size;
  }

  getLevel(level: number): LevelDefinition {
    const entry = this.curated.get(level);
    if (entry !== undefined) {
      return {
        variant: this.variant,
        level,
        seed: entry.seed,
        options: this.options,
        grade: entry.grade,
        hintBudget: entry.hintBudget,
        source: 'curated',
        metrics: entry.metrics,
      };
    }
    return this.generateLevel(level);
  }

  // Beyond the curated range: search deterministic seeds until one is solvable.
  private generateLevel(level: number): LevelDefinition {
    const grade = gradeForLevel(level);
    const klondikeOptions = {
      drawCount: this.options['drawCount'] ?? 1,
      maxRedeals: KLONDIKE_UNLIMITED_REDEALS,
    };
    for (let attempt = 0; attempt < 500; attempt++) {
      const seed = deriveSeed(this.variant, level, attempt);
      const result = solveKlondike(seed, klondikeOptions, this.generationBudget);
      if (result.solved) {
        return {
          variant: this.variant,
          level,
          seed,
          options: this.options,
          grade,
          hintBudget: hintBudgetForGrade(grade),
          source: 'generated',
          metrics: result.metrics,
        };
      }
    }
    throw new Error(`Could not generate a solvable Klondike deal for level ${level}.`);
  }
}

/**
 * Endless provider: deterministic random deals with a proxy-scaled hint budget.
 * `optionsFor` sets the engine options for a level; `hintBudgetFor` derives the
 * hint budget from the difficulty proxy (suit count, level counter, ...).
 */
export class EndlessLevelProvider implements LevelProvider {
  constructor(
    readonly variant: string,
    private readonly optionsFor: (level: number) => Record<string, number>,
    private readonly hintBudgetFor: (
      level: number,
      options: Readonly<Record<string, number>>,
    ) => number,
  ) {}

  getLevel(level: number): LevelDefinition {
    const options = this.optionsFor(level);
    return {
      variant: this.variant,
      level,
      seed: deriveSeed(this.variant, level, 0),
      options,
      grade: gradeForLevel(level),
      hintBudget: this.hintBudgetFor(level, options),
      source: 'generated',
    };
  }
}

/** Spider difficulty proxy: fewer hints as suit count (difficulty) rises. */
export function spiderHintBudget(suitCount: number): number {
  if (suitCount <= 1) {
    return 5;
  }
  return suitCount === 2 ? 3 : 1;
}

/** Generic "running level counter" proxy for the other endless variants. */
export function levelCounterHintBudget(level: number): number {
  return Math.max(1, 5 - Math.floor((level - 1) / 10));
}

/** Spider endless provider for a chosen suit count (the difficulty proxy). */
export function createSpiderEndlessProvider(suitCount: number): EndlessLevelProvider {
  return new EndlessLevelProvider(
    'spider',
    () => ({ suitCount }),
    (_level, options) => spiderHintBudget(options['suitCount'] ?? suitCount),
  );
}

// -- registry -----------------------------------------------------------------

const providers = new Map<string, LevelProvider>();
providers.set('klondike', new KlondikeCuratedProvider());
providers.set('spider', createSpiderEndlessProvider(2));
// FreeCell has no configurable rules (empty options bag) and is solvable nearly
// always, so — like Spider — it ships as endless deterministic deals with a
// running-level-counter hint budget rather than a curated, solver-graded ladder.
providers.set('freecell', new EndlessLevelProvider('freecell', () => ({}), levelCounterHintBudget));
providers.set('pyramid', new EndlessLevelProvider('pyramid', () => ({}), levelCounterHintBudget));
providers.set('tripeaks', new EndlessLevelProvider('tripeaks', () => ({}), levelCounterHintBudget));

export function registerLevelProvider(provider: LevelProvider): void {
  providers.set(provider.variant, provider);
}

export function getLevelProvider(variant: string): LevelProvider {
  const provider = providers.get(variant);
  if (provider === undefined) {
    throw new Error(`No level provider registered for variant '${variant}'.`);
  }
  return provider;
}

export function getLevel(variant: string, level: number): LevelDefinition {
  return getLevelProvider(variant).getLevel(level);
}

/** True if a deterministically generated Klondike deal for a seed is solvable. */
export function isServableKlondikeSeed(
  seed: number,
  drawCount = 1,
  budget: number = DEFAULT_SOLVER_BUDGET,
): boolean {
  return isKlondikeSolvable(seed, { drawCount, maxRedeals: KLONDIKE_UNLIMITED_REDEALS }, budget);
}
