// Registry of the available engines, keyed by variant id — the TS mirror of
// SolitaireEngines.cs. Lets callers resolve an engine and verify any variant
// uniformly through the common SolitaireEngine interface.

import { klondikeEngine } from './klondike';
import { spiderEngine } from './spider';
import type { SolitaireEngine } from './types';

const registry: Readonly<Record<string, SolitaireEngine>> = {
  [klondikeEngine.variant]: klondikeEngine,
  [spiderEngine.variant]: spiderEngine,
};

export const solitaireEngines = {
  /** All supported variant ids. */
  get variants(): string[] {
    return Object.keys(registry);
  },

  /** Gets the engine for a variant id (case-insensitive), or throws if unknown. */
  for(variant: string): SolitaireEngine {
    const engine = registry[variant.toLowerCase()];
    if (engine === undefined) {
      throw new Error(`Unknown solitaire variant '${variant}'.`);
    }
    return engine;
  },

  /** Tries to get the engine for a variant id. */
  tryGet(variant: string): SolitaireEngine | undefined {
    return registry[variant.toLowerCase()];
  },
};
