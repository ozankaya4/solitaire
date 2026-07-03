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
}

export function Segmented<T extends string>({
  value,
  options,
  onChange,
  label,
  block,
}: SegmentedProps<T>) {
  return (
    <div
      className={block ? 'segmented segmented--block' : 'segmented'}
      role="group"
      aria-label={label}
    >
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
