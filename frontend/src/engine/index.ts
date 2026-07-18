// Public surface of the client-side solitaire engines. Behaviorally identical to
// the authoritative C# Solitaire.Engine; drives instant local gameplay while the
// server engine remains the source of truth for score verification.
//
// Conceptual API per variant: new game, apply move, get legal moves, detect win,
// and read the score (state.score). Every variant also implements the common
// SolitaireEngine interface (variant + replay) via `solitaireEngines`.

export * from './cards';
export * from './types';
export { DeterministicRandom } from './random';
export type { TableauPile } from './tableau';
export {
  buildOrdered,
  dealKlondike,
  shuffle,
  shuffleInPlace,
  DECK_SIZE,
  KLONDIKE_TABLEAU_COUNT,
} from './deck';

export {
  klondikeEngine,
  klondikeGetLegalMoves,
  klondikeIsWon,
  klondikeNewGame,
  klondikeOptionsFromBag,
  klondikeReplay,
  klondikeTryApplyMove,
  KLONDIKE_UNLIMITED_REDEALS,
  type KlondikeOptions,
  type KlondikeState,
} from './klondike';

export {
  buildSpiderDeck,
  shuffleSpider,
  spiderEngine,
  spiderGetLegalMoves,
  spiderIsWon,
  spiderNewGame,
  spiderOptionsFromBag,
  spiderReplay,
  spiderTryApplyMove,
  SPIDER_TABLEAU_COUNT,
  SPIDER_TOTAL_SEQUENCES,
  type SpiderOptions,
  type SpiderState,
} from './spider';

export {
  freecellEngine,
  freecellGetLegalMoves,
  freecellIsWon,
  freecellNewGame,
  freecellReplay,
  freecellTryApplyMove,
  FREECELL_FREE_CELL_COUNT,
  FREECELL_TABLEAU_COUNT,
  type FreeCellState,
} from './freecell';

export {
  pyramidEngine,
  pyramidGetLegalMoves,
  pyramidIsWon,
  pyramidNewGame,
  pyramidReplay,
  pyramidTryApplyMove,
  PYRAMID_ROW_COUNT,
  PYRAMID_SIZE,
  PYRAMID_WASTE,
  type PyramidState,
} from './pyramid';

export {
  tripeaksEngine,
  tripeaksGetLegalMoves,
  tripeaksIsWon,
  tripeaksNewGame,
  tripeaksReplay,
  tripeaksTryApplyMove,
  TRIPEAKS_TABLEAU_SIZE,
  type TriPeaksState,
} from './tripeaks';

export { solitaireEngines } from './engines';
