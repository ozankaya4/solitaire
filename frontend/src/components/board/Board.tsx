import { useReducedMotion } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { Pile } from './Pile';
import type { Game } from '../../board/useGame';
import type { BoardModel } from '../../board/boardModel';

/** Cumulative card count before each tableau pile → column-major deal order. */
function dealStarts(model: BoardModel): number[] {
  const starts: number[] = [];
  let running = 0;
  for (const pile of model.tableau) {
    starts.push(running);
    running += pile.cards.length;
  }
  return starts;
}

export function Board({ game }: { game: Game }) {
  const { t } = useTranslation();
  const reduce = useReducedMotion() ?? false;
  const model = game.model;
  if (!model) {
    return null;
  }

  // Layout tweens are enabled for Klondike (unique card keys); Spider has
  // duplicate cards so it renders without per-card layout animation.
  const enableLayout = model.variant === 'klondike' && !reduce;
  const starts = dealStarts(model);

  if (model.variant === 'spider') {
    return (
      <div className="board" data-variant="spider">
        <div className="board__top" style={{ display: 'flex', justifyContent: 'space-between' }}>
          <span className="completed-pill">
            {t('game.completed', { done: model.completed ?? 0, total: 8 })}
          </span>
          {model.stock ? (
            <Pile
              pile={model.stock}
              game={game}
              reduce={reduce}
              enableLayout={false}
              dealStart={-1}
            />
          ) : null}
        </div>
        <div className="board__tableau">
          {model.tableau.map((pile, i) => (
            <Pile
              key={pile.id}
              pile={pile}
              game={game}
              reduce={reduce}
              enableLayout={false}
              dealStart={starts[i] ?? 0}
            />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="board" data-variant="klondike">
      <div className="board__top">
        {model.stock ? (
          <Pile
            pile={model.stock}
            game={game}
            reduce={reduce}
            enableLayout={enableLayout}
            dealStart={-1}
          />
        ) : null}
        {model.waste ? (
          <Pile
            pile={model.waste}
            game={game}
            reduce={reduce}
            enableLayout={enableLayout}
            dealStart={-1}
          />
        ) : null}
        <div className="board__spacer" />
        {model.foundations.map((pile) => (
          <Pile
            key={pile.id}
            pile={pile}
            game={game}
            reduce={reduce}
            enableLayout={enableLayout}
            dealStart={-1}
          />
        ))}
      </div>
      <div className="board__tableau">
        {model.tableau.map((pile, i) => (
          <Pile
            key={pile.id}
            pile={pile}
            game={game}
            reduce={reduce}
            enableLayout={enableLayout}
            dealStart={starts[i] ?? 0}
          />
        ))}
      </div>
    </div>
  );
}
