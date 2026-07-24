// A segmented toggle used for theme / language / draw-mode choices.

import type { ReactNode } from 'react';

export interface SegmentedOption<T extends string> {
  readonly value: T;
  readonly label: string;
  readonly icon?: ReactNode;
  /** BCP-47 tag when the label's language differs from the UI (fixes casing, e.g. Turkish İ). */
  readonly lang?: string;
}

interface SegmentedProps<T extends string> {
  readonly value: T;
  readonly options: readonly SegmentedOption<T>[];
  readonly onChange: (value: T) => void;
  readonly label: string;
  readonly block?: boolean;
  /** Wrap options onto multiple rows (for pickers with many options, e.g. the leaderboard). */
  readonly wrap?: boolean;
}

export function Segmented<T extends string>({
  value,
  options,
  onChange,
  label,
  block,
  wrap,
}: SegmentedProps<T>) {
  const classes = ['segmented'];
  if (block) {
    classes.push('segmented--block');
  }
  if (wrap) {
    classes.push('segmented--wrap');
  }
  return (
    <div className={classes.join(' ')} role="group" aria-label={label}>
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          className="seg"
          aria-pressed={option.value === value}
          onClick={() => onChange(option.value)}
        >
          {option.icon}
          <span lang={option.lang}>{option.label}</span>
        </button>
      ))}
    </div>
  );
}
