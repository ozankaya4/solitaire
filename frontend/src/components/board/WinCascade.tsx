import { useMemo } from 'react';
import { motion, useReducedMotion } from 'motion/react';
import { useTranslation } from 'react-i18next';

interface CascadeCard {
  readonly id: number;
  readonly left: number;
  readonly drift: number;
  readonly spin: number;
  readonly delay: number;
}

export function WinCascade({ onNext, onMenu }: { onNext: () => void; onMenu: () => void }) {
  const { t } = useTranslation();
  const reduce = useReducedMotion() ?? false;

  const cards = useMemo<CascadeCard[]>(() => {
    if (reduce) {
      return [];
    }
    const width = typeof window === 'undefined' ? 400 : window.innerWidth;
    return Array.from({ length: 26 }, (_, id) => ({
      id,
      left: 4 + Math.random() * 90,
      drift: (Math.random() * 2 - 1) * width * 0.16,
      spin: (Math.random() * 2 - 1) * 240,
      delay: id * 0.05,
    }));
  }, [reduce]);

  const floor = (typeof window === 'undefined' ? 700 : window.innerHeight) * 0.82;

  return (
    <>
      {cards.map((card) => (
        <motion.div
          key={card.id}
          className="cascade-card"
          style={{ left: `${card.left}%` }}
          initial={{ y: 0, x: 0, rotate: 0, opacity: 0 }}
          animate={{
            y: [0, floor, floor - 90, floor, floor - 36, floor],
            x: [0, card.drift],
            rotate: [0, card.spin],
            opacity: [0, 1, 1, 1, 1, 1],
          }}
          transition={{
            duration: 2,
            delay: card.delay,
            ease: 'easeIn',
            times: [0, 0.45, 0.6, 0.78, 0.9, 1],
            repeat: Infinity,
            repeatDelay: 1.1,
          }}
        >
          <div className="card__back" style={{ position: 'absolute', inset: 0 }}>
            <span className="card__emblem" />
          </div>
        </motion.div>
      ))}

      <div className="win" role="dialog" aria-modal="true" aria-label={t('game.win')}>
        <motion.div
          className="win__panel"
          initial={reduce ? false : { opacity: 0, scale: 0.9, y: 8 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          transition={reduce ? { duration: 0 } : { duration: 0.35, ease: [0.16, 0.9, 0.1, 1] }}
        >
          <h2 className="win__title">{t('game.win')}</h2>
          <p className="win__sub">{t('game.winSub')}</p>
          <button type="button" className="btn btn--primary btn--block" onClick={onNext}>
            {t('game.next')}
          </button>
          <button
            type="button"
            className="btn btn--block"
            style={{ marginTop: 'var(--space-sm)' }}
            onClick={onMenu}
          >
            {t('game.toMenu')}
          </button>
        </motion.div>
      </div>
    </>
  );
}
