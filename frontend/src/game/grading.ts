// Difficulty grading: turns solver metrics into a difficulty score, and maps
// curated level numbers to grade buckets and hint budgets.

import type { SolveMetrics } from '../engine/solver';

export type Grade = 'easy' | 'medium' | 'hard';

/** Curated ladder buckets: levels 1–20 easy, 21–40 medium, 41+ harder. */
export const EASY_MAX_LEVEL = 20;
export const MEDIUM_MAX_LEVEL = 40;

export function gradeForLevel(level: number): Grade {
  if (level <= EASY_MAX_LEVEL) {
    return 'easy';
  }
  if (level <= MEDIUM_MAX_LEVEL) {
    return 'medium';
  }
  return 'hard';
}

/** Hint budgets: generous on easy, few on hard. */
export const HINT_BUDGET_BY_GRADE: Readonly<Record<Grade, number>> = {
  easy: 5,
  medium: 3,
  hard: 1,
};

export function hintBudgetForGrade(grade: Grade): number {
  return HINT_BUDGET_BY_GRADE[grade];
}

/**
 * Difficulty score for a solved deal. Higher = harder. Combines how much the
 * bounded solver had to search (nodesExpanded — the dominant term) with the
 * solution length (longer lines are fiddlier to play). This is a documented
 * heuristic proxy, not an exact difficulty measure; curated deals are ranked by
 * it and bucketed by rank into the ladder above.
 */
export function difficultyScore(metrics: SolveMetrics): number {
  return metrics.nodesExpanded + metrics.solutionLength;
}
