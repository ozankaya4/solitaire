// Offline build-time generator for the curated Klondike level ladder.
//
// Runs the bounded solver over candidate seeds, keeps the solvable ones, ranks
// them by difficulty score, and buckets them into the ladder (1–20 easy,
// 21–40 medium, 41+ harder). Emits src/levels/klondike.levels.json which ships
// with the frontend. Run with: npm run gen:levels
//
// Because the solver is bounded (see engine/solver.ts), unsolvable-within-budget
// seeds are simply skipped; the ladder only contains deals with a verified win.

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { KLONDIKE_UNLIMITED_REDEALS } from '../src/engine/klondike';
import { solveKlondike } from '../src/engine/solver';
import { difficultyScore, gradeForLevel, hintBudgetForGrade } from '../src/game/grading';

const OPTIONS = { drawCount: 1, maxRedeals: KLONDIKE_UNLIMITED_REDEALS };
const SOLVER_BUDGET = 200_000;
const TARGET_LEVELS = 45; // 20 easy + 20 medium + 5 hard
const MAX_SEED = 600;

interface Solved {
  readonly seed: number;
  readonly metrics: ReturnType<typeof solveKlondike>['metrics'];
}

const solved: Solved[] = [];
for (let seed = 1; seed <= MAX_SEED && solved.length < TARGET_LEVELS; seed++) {
  const result = solveKlondike(seed, OPTIONS, SOLVER_BUDGET);
  if (result.solved) {
    solved.push({ seed, metrics: result.metrics });
  }
}

if (solved.length < TARGET_LEVELS) {
  throw new Error(`Only found ${solved.length}/${TARGET_LEVELS} solvable deals; raise MAX_SEED.`);
}

// Rank by difficulty (ascending) so level number reflects difficulty.
solved.sort(
  (a, b) =>
    difficultyScore(a.metrics) - difficultyScore(b.metrics) ||
    a.metrics.nodesExpanded - b.metrics.nodesExpanded ||
    a.seed - b.seed,
);

const levels = solved.map((entry, index) => {
  const level = index + 1;
  const grade = gradeForLevel(level);
  return {
    level,
    seed: entry.seed,
    grade,
    hintBudget: hintBudgetForGrade(grade),
    metrics: entry.metrics,
  };
});

const library = {
  variant: 'klondike',
  solverBudget: SOLVER_BUDGET,
  options: OPTIONS,
  levels,
};

const outDir = resolve(dirname(fileURLToPath(import.meta.url)), '../src/levels');
mkdirSync(outDir, { recursive: true });
const outPath = resolve(outDir, 'klondike.levels.json');
writeFileSync(outPath, `${JSON.stringify(library, null, 2)}\n`);

const counts = { easy: 0, medium: 0, hard: 0 };
for (const l of levels) {
  counts[l.grade]++;
}
// eslint-disable-next-line no-console
console.log(`Wrote ${levels.length} levels to ${outPath}`, counts);
