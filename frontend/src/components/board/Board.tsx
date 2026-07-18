// The board renders two layers inside one measured "stage":
//  1. drop ZONES — positioned hit/outline areas per pile (also the tap targets), and
//  2. a flat CARD layer — every card as a CardNode keyed by card identity.
// All geometry is computed in pixels from the measured width, so cards can be
// animated between any two slots without ever re-parenting DOM nodes.

import { useCallback, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useReducedMotion, type PanInfo } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { CardNode, type NodeSpec, type Registry } from './CardNode';
import { RefreshIcon } from '../../icons/icons';
import { findPile } from '../../board/moves';
import type { BoardModel, Pile } from '../../board/boardModel';
import type { Game } from '../../board/useGame';

interface Zone {
  readonly id: string;
  readonly x: number;
  readonly y: number;
  readonly w: number;
  readonly h: number;
  /** Draw a dashed slot outline (empty pile / stock). */
  readonly slot: boolean;
  /** Height of the outline when the zone extends past it (tableau columns). */
  readonly slotH: number;
}

interface Layout {
  readonly cardW: number;
  readonly cardH: number;
  readonly stageW: number;
  readonly stageH: number;
  readonly zones: readonly Zone[];
  readonly specs: readonly NodeSpec[];
}

function useMeasuredWidth(): [React.RefObject<HTMLDivElement | null>, number] {
  const ref = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(0);
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) {
      return undefined;
    }
    const observer = new ResizeObserver((entries) => {
      const w = entries[0]?.contentRect.width ?? 0;
      setWidth((prev) => (Math.abs(prev - w) > 0.5 ? w : prev));
    });
    observer.observe(el);
    setWidth(el.clientWidth);
    return () => observer.disconnect();
  }, []);
  return [ref, width];
}

function stackedPileSpecs(
  pile: Pile,
  pos: { x: number; y: number },
  options: { topDraggable: boolean; stock?: boolean; fan?: number; animateMoves: boolean },
): NodeSpec[] {
  const n = pile.cards.length;
  const fanStart = Math.max(0, n - 3);
  return pile.cards.map((rc, index) => ({
    key: rc.key,
    card: rc.card,
    faceUp: rc.faceUp,
    // Waste fan: the last three drawn cards spread right so the card beneath the
    // top one stays visible (especially while the top card is being dragged).
    x: pos.x + (options.fan && index >= fanStart ? (index - fanStart) * options.fan : 0),
    y: pos.y,
    // Stock arrays are top-first; visual stacks are bottom-first.
    z: options.stock ? n - index : index + 1,
    pileId: pile.id,
    index,
    draggable: options.topDraggable && index === n - 1 && rc.faceUp,
    stockTap: (options.stock ?? false) && index === 0,
    dealOrder: -1,
    dealFrom: null,
    animateMoves: options.animateMoves,
  }));
}

function tableauSpecs(
  pile: Pile,
  x: number,
  tabY: number,
  stepUp: number,
  stepDown: number,
  dealStart: number,
  dealFrom: { x: number; y: number },
  animateMoves: boolean,
): { specs: NodeSpec[]; bottom: number } {
  const specs: NodeSpec[] = [];
  let offset = 0;
  pile.cards.forEach((rc, index) => {
    specs.push({
      key: rc.key,
      card: rc.card,
      faceUp: rc.faceUp,
      x,
      y: tabY + offset,
      z: index + 1,
      pileId: pile.id,
      index,
      draggable: rc.faceUp,
      stockTap: false,
      dealOrder: dealStart + index,
      dealFrom,
      animateMoves,
    });
    offset += rc.faceUp ? stepUp : stepDown;
  });
  const lastY = pile.cards.length > 0 ? specs[specs.length - 1]!.y : tabY;
  return { specs, bottom: lastY };
}

function buildLayout(model: BoardModel, width: number): Layout {
  const spider = model.variant === 'spider';
  const freecell = model.variant === 'freecell';
  const cols = spider ? 10 : freecell ? 8 : 7;
  const gap = Math.max(4, Math.round(width * 0.012));
  const rawW = Math.floor((width - gap * (cols - 1)) / cols);
  const cardW = spider ? Math.max(36, Math.min(52, rawW)) : Math.min(64, rawW);
  const cardH = Math.round(cardW * 1.42);
  const stepUp = Math.round(cardH * 0.34);
  const stepDown = Math.round(cardH * 0.16);
  const rowGap = Math.round(cardH * 0.4);
  const colX = (i: number): number => i * (cardW + gap);
  const stageW = Math.max(width, colX(cols - 1) + cardW);
  const tabY = cardH + rowGap;
  // Where dealt cards visually originate from. Klondike/Spider have a real
  // stock pile to fly in from; FreeCell has none, so the top-left corner (where
  // the first free cell sits) is used as an unobtrusive, self-consistent origin.
  const stockPos = spider ? { x: stageW - cardW, y: 0 } : { x: colX(0), y: 0 };
  const animateMoves = !spider;

  const specs: NodeSpec[] = [];
  const zones: Zone[] = [];

  if (model.stock) {
    specs.push(
      ...stackedPileSpecs(model.stock, stockPos, {
        topDraggable: false,
        stock: true,
        animateMoves,
      }),
    );
    zones.push({ id: 'stock', ...stockPos, w: cardW, h: cardH, slot: true, slotH: cardH });
  }
  if (model.waste) {
    const fan = model.drawCount === 3 ? Math.round(cardW * 0.32) : 0;
    const pos = { x: colX(1), y: 0 };
    specs.push(...stackedPileSpecs(model.waste, pos, { topDraggable: true, fan, animateMoves }));
    zones.push({
      id: 'waste',
      ...pos,
      w: cardW + fan * 2,
      h: cardH,
      slot: model.waste.cards.length === 0,
      slotH: cardH,
    });
  }
  if (model.freeCells) {
    model.freeCells.forEach((pile, slot) => {
      const pos = { x: colX(slot), y: 0 };
      specs.push(...stackedPileSpecs(pile, pos, { topDraggable: true, animateMoves }));
      zones.push({ id: pile.id, ...pos, w: cardW, h: cardH, slot: true, slotH: cardH });
    });
  }
  model.foundations.forEach((pile, slot) => {
    // FreeCell's foundations sit right of its 4 free cells; Klondike leaves a
    // one-column gap after stock+waste before its foundations begin.
    const pos = { x: colX(freecell ? 4 + slot : 3 + slot), y: 0 };
    specs.push(...stackedPileSpecs(pile, pos, { topDraggable: true, animateMoves }));
    zones.push({
      id: pile.id,
      ...pos,
      w: cardW,
      h: cardH,
      // Always draw the outline: it sits behind the card and is revealed when the
      // top card (e.g. a lone ace) is lifted, showing the slot beneath.
      slot: true,
      slotH: cardH,
    });
  });

  let dealStart = 0;
  let maxBottom = tabY;
  model.tableau.forEach((pile, col) => {
    const built = tableauSpecs(
      pile,
      colX(col),
      tabY,
      stepUp,
      stepDown,
      dealStart,
      stockPos,
      animateMoves,
    );
    specs.push(...built.specs);
    dealStart += pile.cards.length;
    maxBottom = Math.max(maxBottom, built.bottom);
  });
  const stageH = maxBottom + cardH + Math.round(cardH * 0.5);
  model.tableau.forEach((pile, col) => {
    zones.push({
      id: pile.id,
      x: colX(col),
      y: tabY,
      w: cardW,
      h: stageH - tabY,
      // Always draw the column-start outline: it sits behind the cards and is
      // revealed when the column empties (rather than popping in).
      slot: true,
      slotH: cardH,
    });
  });

  return { cardW, cardH, stageW, stageH, zones, specs };
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

export function Board({ game }: { game: Game }) {
  const { t } = useTranslation();
  const reduce = useReducedMotion() ?? false;
  const [scrollRef, width] = useMeasuredWidth();
  const model = game.model;

  const layout = useMemo<Layout | null>(
    () => (model && width > 0 ? buildLayout(model, width) : null),
    [model, width],
  );

  const registryRef = useRef<Registry>(new Map());
  const unsubscribeRef = useRef<(() => void)[]>([]);
  const [runKeys, setRunKeys] = useState<ReadonlySet<string> | null>(null);
  const [dragEndNonce, setDragEndNonce] = useState(0);

  // Drag a whole run: cards below the grabbed one mirror the leader's motion
  // values (with their fan offsets) for the duration of the gesture.
  const onRunStart = useCallback(
    (spec: NodeSpec) => {
      const followers =
        layout?.specs.filter((s) => s.pileId === spec.pileId && s.index > spec.index) ?? [];
      const leader = registryRef.current.get(spec.key);
      const subs: (() => void)[] = [];
      if (leader) {
        for (const follower of followers) {
          const values = registryRef.current.get(follower.key);
          if (!values) {
            continue;
          }
          const dx = follower.x - spec.x;
          const dy = follower.y - spec.y;
          subs.push(leader.x.on('change', (v) => values.x.set(v + dx)));
          subs.push(leader.y.on('change', (v) => values.y.set(v + dy)));
        }
      }
      unsubscribeRef.current = subs;
      setRunKeys(new Set([spec.key, ...followers.map((f) => f.key)]));
    },
    [layout],
  );

  const onRunEnd = useCallback(
    (spec: NodeSpec, info: PanInfo) => {
      for (const unsubscribe of unsubscribeRef.current) {
        unsubscribe();
      }
      unsubscribeRef.current = [];
      const destId = hitTestPile(info);
      if (destId && destId !== spec.pileId) {
        game.onDrop(spec.pileId, spec.index, destId);
      }
      setRunKeys(null);
      // Re-settle every card (legal drop → new slots; illegal → back home).
      setDragEndNonce((n) => n + 1);
    },
    [game],
  );

  // Hint highlights: outline the zones, plus the top card of the source pile.
  const { hintZones, hintKeys } = useMemo(() => {
    const zonesSet = new Set<string>();
    const keys = new Set<string>();
    if (game.hint && model && layout) {
      const zoneIds = new Set(layout.zones.map((z) => z.id));
      for (const id of [game.hint.sourceId, game.hint.destId]) {
        if (!id) {
          continue;
        }
        // An unassigned foundation suit maps to the first empty slot.
        const zoneId = zoneIds.has(id)
          ? id
          : id.startsWith('foundation-')
            ? layout.zones.find((z) => z.id.startsWith('fslot-'))?.id
            : undefined;
        if (zoneId) {
          zonesSet.add(zoneId);
          const pile = findPile(model, zoneId);
          const top = pile?.cards[pile.cards.length - 1];
          if (top) {
            keys.add(top.key);
          }
        }
      }
    }
    return { hintZones: zonesSet, hintKeys: keys };
  }, [game.hint, model, layout]);

  if (!model) {
    return null;
  }

  const stockEmpty = (model.stock?.cards.length ?? 0) === 0;
  const stageStyle = layout
    ? ({
        height: layout.stageH,
        width: model.variant === 'spider' ? layout.stageW : undefined,
        '--card-w': `${layout.cardW}px`,
        '--card-h': `${layout.cardH}px`,
      } as React.CSSProperties)
    : undefined;

  return (
    <div className="board" data-variant={model.variant}>
      {model.variant === 'spider' ? (
        <div className="board__meta">
          <span className="completed-pill">
            {t('game.completed', { done: model.completed ?? 0, total: 8 })}
          </span>
        </div>
      ) : null}

      <div className="board__scroll" ref={scrollRef}>
        <div className="board__stage" style={stageStyle}>
          {layout?.zones.map((zone) => (
            <div
              key={zone.id}
              data-pile-id={zone.id}
              className={`zone${hintZones.has(zone.id) ? ' is-hint' : ''}`}
              style={{ left: zone.x, top: zone.y, width: zone.w, height: zone.h }}
              onClick={() => (zone.id === 'stock' ? game.onStock() : game.onPileTap(zone.id))}
              role={zone.id === 'stock' ? 'button' : undefined}
              tabIndex={zone.id === 'stock' ? 0 : undefined}
              aria-label={zone.id === 'stock' ? t('game.stock') : undefined}
              onKeyDown={
                zone.id === 'stock'
                  ? (e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        game.onStock();
                      }
                    }
                  : undefined
              }
            >
              {zone.slot ? (
                <div className="zone__slot" style={{ height: zone.slotH }}>
                  {zone.id === 'stock' && stockEmpty ? <RefreshIcon size={22} /> : null}
                </div>
              ) : null}
            </div>
          ))}

          {layout?.specs
            .slice()
            .sort((a, b) => (a.key < b.key ? -1 : 1))
            .map((spec) => (
              <CardNode
                key={spec.key}
                spec={spec}
                reduce={reduce}
                registry={registryRef.current}
                selected={
                  game.selected?.pileId === spec.pileId && game.selected.index === spec.index
                }
                hinted={hintKeys.has(spec.key)}
                inRun={runKeys?.has(spec.key) ?? false}
                dragEndNonce={dragEndNonce}
                onRunStart={onRunStart}
                onRunEnd={onRunEnd}
                onTapCard={game.onCardTap}
                onDoubleTapCard={game.onCardDoubleTap}
                onStockTap={game.onStock}
              />
            ))}
        </div>
      </div>
    </div>
  );
}
