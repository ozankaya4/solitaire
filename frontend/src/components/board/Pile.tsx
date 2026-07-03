import { useState } from 'react';
import { motion, type PanInfo } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { Card } from './Card';
import { RefreshIcon } from '../../icons/icons';
import type { Pile as PileModel, RenderCard } from '../../board/boardModel';
import type { Game } from '../../board/useGame';

const DEAL_EASE = [0.16, 0.9, 0.1, 1] as const;

interface PileProps {
  pile: PileModel;
  game: Game;
  reduce: boolean;
  enableLayout: boolean;
  /** Deal-order offset for the first card in this pile; -1 disables the deal entrance. */
  dealStart: number;
}

function hitTestPile(info: PanInfo): string | undefined {
  const els = document.elementsFromPoint(info.point.x, info.point.y);
  for (const el of els) {
    if (el instanceof HTMLElement && el.dataset.pileId) {
      return el.dataset.pileId;
    }
  }
  return undefined;
}

function CardSlot({
  pile,
  index,
  rc,
  cum,
  game,
  reduce,
  enableLayout,
  dealOrder,
}: {
  pile: PileModel;
  index: number;
  rc: RenderCard;
  cum: number;
  game: Game;
  reduce: boolean;
  enableLayout: boolean;
  dealOrder: number;
}) {
  const [dragging, setDragging] = useState(false);
  const isTop = index === pile.cards.length - 1;
  const draggable =
    rc.faceUp &&
    (pile.kind === 'tableau' || ((pile.kind === 'waste' || pile.kind === 'foundation') && isTop));
  const selected = game.selected?.pileId === pile.id && game.selected.index === index;

  const classes = ['pile__card'];
  if (draggable) classes.push('is-draggable');
  if (selected) classes.push('is-selected');
  if (dragging) classes.push('is-dragging');

  return (
    <motion.div
      className={classes.join(' ')}
      style={{ top: `calc(var(--u) * ${cum})`, zIndex: dragging ? 100 : index + 1 }}
      layout={enableLayout}
      layoutId={enableLayout ? rc.key : undefined}
      drag={draggable}
      dragSnapToOrigin
      whileDrag={reduce ? undefined : { scale: 1.06 }}
      onDragStart={() => setDragging(true)}
      onDragEnd={(_event, info) => {
        setDragging(false);
        const destId = hitTestPile(info);
        if (destId) {
          game.onDrop(pile.id, index, destId);
        }
      }}
      onTap={() => {
        if (draggable) {
          game.onCardTap(pile.id, index);
        }
      }}
      onDoubleClick={() => {
        if (draggable) {
          game.onCardDoubleTap(pile.id, index);
        }
      }}
      initial={reduce || dealOrder < 0 ? false : { opacity: 0, x: -40, y: -90, rotate: -6 }}
      animate={{ opacity: 1, x: 0, y: 0, rotate: 0 }}
      transition={
        reduce
          ? { duration: 0 }
          : { delay: dealOrder < 0 ? 0 : dealOrder * 0.028, duration: 0.34, ease: DEAL_EASE }
      }
    >
      <Card card={rc.card} faceUp={rc.faceUp} />
    </motion.div>
  );
}

export function Pile({ pile, game, reduce, enableLayout, dealStart }: PileProps) {
  const { t } = useTranslation();
  const isHint = game.hint?.sourceId === pile.id || game.hint?.destId === pile.id;
  const empty = pile.cards.length === 0;

  // Stock: click to draw / deal / recycle.
  if (pile.kind === 'stock') {
    return (
      <div
        className={`pile pile--slot${isHint ? ' is-hint' : ''}`}
        data-pile-id={pile.id}
        onClick={() => game.onStock()}
        role="button"
        tabIndex={0}
        aria-label={t('game.stock')}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') game.onStock();
        }}
      >
        {empty ? (
          <RefreshIcon size={22} className="pile__stockglyph" />
        ) : (
          <Card card={pile.cards[0]!.card} faceUp={false} />
        )}
      </div>
    );
  }

  // Single-slot piles (waste / foundation): show only the top card.
  if (pile.kind === 'waste' || pile.kind === 'foundation') {
    const top = pile.cards[pile.cards.length - 1];
    return (
      <div
        className={`pile${empty ? ' pile--slot' : ''}${isHint ? ' is-hint' : ''}`}
        data-pile-id={pile.id}
        onClick={(e) => {
          if (e.currentTarget === e.target) game.onPileTap(pile.id);
        }}
      >
        {top ? (
          <div className="pile__card" style={{ position: 'relative' }}>
            <CardSlot
              pile={pile}
              index={pile.cards.length - 1}
              rc={top}
              cum={0}
              game={game}
              reduce={reduce}
              enableLayout={enableLayout}
              dealOrder={-1}
            />
          </div>
        ) : null}
      </div>
    );
  }

  // Tableau: a fanned column.
  const cums: number[] = [];
  let running = 0;
  for (let i = 0; i < pile.cards.length; i++) {
    cums.push(running);
    running += pile.cards[i]!.faceUp ? 2 : 1;
  }
  const lastCum = cums.length > 0 ? cums[cums.length - 1]! : 0;

  return (
    <div
      className={`pile${empty ? ' pile--slot' : ''}${isHint ? ' is-hint' : ''}`}
      data-pile-id={pile.id}
      style={{ minHeight: `calc(var(--card-h) + var(--u) * ${lastCum})` }}
      onClick={(e) => {
        if (e.currentTarget === e.target) game.onPileTap(pile.id);
      }}
    >
      {pile.cards.map((rc, index) => (
        <CardSlot
          key={rc.key}
          pile={pile}
          index={index}
          rc={rc}
          cum={cums[index]!}
          game={game}
          reduce={reduce}
          enableLayout={enableLayout}
          dealOrder={dealStart < 0 ? -1 : dealStart + index}
        />
      ))}
    </div>
  );
}
