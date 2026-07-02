import { describe, expect, it } from 'vitest';
import klondikeLibrary from '../../levels/klondike.levels.json';
import { difficultyScore, gradeForLevel, hintBudgetForGrade } from '../grading';

describe('gradeForLevel buckets', () => {
  it('maps 1–20 to easy', () => {
    expect(gradeForLevel(1)).toBe('easy');
    expect(gradeForLevel(20)).toBe('easy');
  });

  it('maps 21–40 to medium', () => {
    expect(gradeForLevel(21)).toBe('medium');
    expect(gradeForLevel(40)).toBe('medium');
  });

  it('maps 41+ to hard', () => {
    expect(gradeForLevel(41)).toBe('hard');
    expect(gradeForLevel(999)).toBe('hard');
  });
});

describe('hintBudgetForGrade', () => {
  it('is generous on easy and scarce on hard', () => {
    expect(hintBudgetForGrade('easy')).toBe(5);
    expect(hintBudgetForGrade('medium')).toBe(3);
    expect(hintBudgetForGrade('hard')).toBe(1);
    expect(hintBudgetForGrade('easy')).toBeGreaterThan(hintBudgetForGrade('hard'));
  });
});

describe('difficultyScore', () => {
  it('combines search effort and solution length; higher = harder', () => {
    const easy = difficultyScore({ nodesExpanded: 150, solutionLength: 150, maxBranching: 6 });
    const hard = difficultyScore({ nodesExpanded: 90000, solutionLength: 300, maxBranching: 12 });
    expect(hard).toBeGreaterThan(easy);
    expect(difficultyScore({ nodesExpanded: 100, solutionLength: 50, maxBranching: 4 })).toBe(150);
  });
});

describe('curated Klondike library is correctly bucketed', () => {
  const levels = klondikeLibrary.levels;

  it('has enough levels to fill every bucket', () => {
    expect(levels.length).toBeGreaterThanOrEqual(41);
  });

  it('numbers levels contiguously from 1', () => {
    levels.forEach((entry, index) => {
      expect(entry.level).toBe(index + 1);
    });
  });

  it('assigns each level the grade and hint budget for its ladder position', () => {
    for (const entry of levels) {
      const grade = gradeForLevel(entry.level);
      expect(entry.grade).toBe(grade);
      expect(entry.hintBudget).toBe(hintBudgetForGrade(grade));
    }
  });

  it('is ranked by non-decreasing difficulty score', () => {
    for (let i = 1; i < levels.length; i++) {
      const prev = difficultyScore(levels[i - 1]!.metrics);
      const curr = difficultyScore(levels[i]!.metrics);
      expect(curr).toBeGreaterThanOrEqual(prev);
    }
  });

  it('uses distinct seeds', () => {
    const seeds = new Set(levels.map((l) => l.seed));
    expect(seeds.size).toBe(levels.length);
  });

  it('contains all three buckets', () => {
    const grades = new Set(levels.map((l) => l.grade));
    expect(grades).toContain('easy');
    expect(grades).toContain('medium');
    expect(grades).toContain('hard');
  });
});
