// One card in the flat board layer. Cards live in a single absolutely-positioned
// layer keyed by card identity, so a moved card NEVER unmounts — it animates from
// wherever it currently is (including the exact drop point of a drag) to its new
// slot. This is what makes draws, drops, snap-backs, and undo read as one
// continuous motion instead of disappear/reappear.

import { useEffect, useRef, useState } from 'react';
import { animate, motion, useMotionValue, type MotionValue, type PanInfo } from 'motion/react';
import { Card } from './Card';
import type { Card as CardValue } from '../../engine/cards';

export interface NodeSpec {
  readonly key: string;
  readonly card: CardValue;
  readonly faceUp: boolean;
  /** Target position in stage coordinates. */
  readonly x: number;
  readonly y: number;
  /** Base stacking order (drag/motion temporarily elevates above everything). */
  readonly z: number;
  readonly pileId: string;
  readonly index: number;
  readonly draggable: boolean;
  /** Top card of the stock: a tap draws/deals. */
  readonly stockTap: boolean;
  /** Staggered deal entrance order; -1 = appear in place. */
  readonly dealOrder: number;
  readonly dealFrom: { readonly x: number; readonly y: number } | null;
  /** False disables move tweens (Spider's occurrence keys aren't stable enough). */
  readonly animateMoves: boolean;
}

export type Registry = Map<string, { x: MotionValue<number>; y: MotionValue<number> }>;

interface CardNodeProps {
  spec: NodeSpec;
  reduce: boolean;
  selected: boolean;
  hinted: boolean;
  /** True while this card follows a dragged run leader. */
  inRun: boolean;
  /** Bumped after every drag ends so cards re-settle onto their targets. */
  dragEndNonce: number;
  registry: Registry;
  onRunStart: (spec: NodeSpec) => void;
  onRunEnd: (spec: NodeSpec, info: PanInfo) => void;
  onTapCard: (pileId: string, index: number) => void;
  onDoubleTapCard: (pileId: string, index: number) => void;
  onStockTap: () => void;
}

const MOVE_EASE = [0.16, 0.9, 0.1, 1] as const;
const MOVE_SECONDS = 0.32;
const DEAL_STAGGER = 0.03;

export function CardNode({
  spec,
  reduce,
  selected,
  hinted,
  inRun,
  dragEndNonce,
  registry,
  onRunStart,
  onRunEnd,
  onTapCard,
  onDoubleTapCard,
  onStockTap,
}: CardNodeProps) {
  const start = !reduce && spec.dealFrom ? spec.dealFrom : { x: spec.x, y: spec.y };
  const x = useMotionValue(start.x);
  const y = useMotionValue(start.y);
  const [dragging, setDragging] = useState(false);
  const [moving, setMoving] = useState(false);
  const firstRun = useRef(true);

  useEffect(() => {
    registry.set(spec.key, { x, y });
    return () => {
      registry.delete(spec.key);
    };
  }, [registry, spec.key, x, y]);

  // Settle onto the target whenever it changes (or after a drag releases the card
  // somewhere else). Animates from the CURRENT value — no jumps, no unmounts.
  useEffect(() => {
    if (dragging) {
      return undefined;
    }
    const first = firstRun.current;
    firstRun.current = false;
    if (x.get() === spec.x && y.get() === spec.y) {
      return undefined;
    }
    if (reduce || (!spec.animateMoves && !first)) {
      x.set(spec.x);
      y.set(spec.y);
      return undefined;
    }
    const delay = first && spec.dealOrder >= 0 ? spec.dealOrder * DEAL_STAGGER : 0;
    setMoving(true);
    const ax = animate(x, spec.x, { duration: MOVE_SECONDS, ease: MOVE_EASE, delay });
    const ay = animate(y, spec.y, {
      duration: MOVE_SECONDS,
      ease: MOVE_EASE,
      delay,
      onComplete: () => setMoving(false),
    });
    return () => {
      ax.stop();
      ay.stop();
    };
  }, [spec, dragging, dragEndNonce, reduce, x, y]);

  const elevated = dragging || inRun || moving;
  const classes = ['cardnode'];
  if (spec.draggable) {
    classes.push('is-draggable');
  }
  if (selected) {
    classes.push('is-selected');
  }
  if (hinted) {
    classes.push('is-hint');
  }

  return (
    <motion.div
      className={classes.join(' ')}
      drag={spec.draggable}
      dragMomentum={false}
      whileDrag={reduce ? undefined : { scale: 1.05 }}
      onDragStart={() => {
        setDragging(true);
        onRunStart(spec);
      }}
      onDragEnd={(_event, info) => {
        onRunEnd(spec, info);
        setDragging(false);
      }}
      onTap={() => {
        if (spec.stockTap) {
          onStockTap();
        } else if (spec.draggable) {
          onTapCard(spec.pileId, spec.index);
        }
      }}
      onDoubleClick={() => {
        if (spec.draggable) {
          onDoubleTapCard(spec.pileId, spec.index);
        }
      }}
      style={{ x, y, zIndex: spec.z + (elevated ? 1000 : 0) }}
    >
      <Card card={spec.card} faceUp={spec.faceUp} />
    </motion.div>
  );
}
