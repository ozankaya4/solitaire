import type { VariantId } from './types';

export interface VariantMeta {
  readonly id: VariantId;
  /** Marked in the UI as the most popular / recommended pick. */
  readonly popular?: boolean;
}

/** Order shown in menus. Klondike is the default and flagged most popular. */
export const VARIANTS: readonly VariantMeta[] = [
  { id: 'klondike', popular: true },
  { id: 'spider' },
  { id: 'freecell' },
  { id: 'pyramid' },
  { id: 'tripeaks' },
];
