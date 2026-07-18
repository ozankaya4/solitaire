import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Board } from './Board';
import { WinCascade } from './WinCascade';
import { useGame, type GameResult } from '../../board/useGame';
import { submitGameWithRetry } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { AuthModal } from '../AuthModal';
import { LeaderboardModal } from '../LeaderboardModal';
import type { VariantId } from '../../app/types';
import { ArrowLeftIcon, BulbIcon, GridIcon, RefreshIcon, UndoIcon } from '../../icons/icons';
import { variantIcon } from '../variantIcon';

const PLAYABLE: readonly VariantId[] = ['klondike', 'spider', 'freecell', 'pyramid'];

export function GameScreen({
  onExit,
  initialVariant,
}: {
  onExit: () => void;
  initialVariant?: VariantId;
}) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const game = useGame(initialVariant);
  const [menuOpen, setMenuOpen] = useState(false);
  const [showLeaderboard, setShowLeaderboard] = useState(false);
  const [showAuth, setShowAuth] = useState(false);
  const [rank, setRank] = useState<number | null>(null);
  const [saveFailed, setSaveFailed] = useState(false);
  const submittedRef = useRef<GameResult | null>(null);

  // Submit a won game to the leaderboard when signed in. Re-runs if the player
  // signs in from the win screen (user changes), so a late sign-in still records.
  useEffect(() => {
    const win = game.lastWin;
    if (!win) {
      submittedRef.current = null;
      setRank(null);
      setSaveFailed(false);
      return undefined;
    }
    if (!user || submittedRef.current === win) {
      return undefined;
    }
    submittedRef.current = win;
    setSaveFailed(false);
    let active = true;
    submitGameWithRetry({
      variant: win.variant,
      seed: win.seed,
      level: win.level,
      options: win.options,
      moves: win.moves,
      claimedScore: win.score,
      claimedTimeMs: win.timeMs,
    })
      .then((result) => {
        if (active) {
          setRank(result.rank);
        }
      })
      .catch(() => {
        // Never fail silently: the player deserves to know the level did not
        // reach the board (and it stays retryable).
        if (active) {
          setSaveFailed(true);
          submittedRef.current = null;
        }
      });
    return () => {
      active = false;
    };
  }, [game.lastWin, user]);

  if (!game.supported) {
    return (
      <div className="game">
        <div className="gamebar">
          <button type="button" className="iconbtn" onClick={onExit} aria-label={t('a11y.back')}>
            <ArrowLeftIcon size={20} />
          </button>
          <div className="gamebar__meta">
            <span className="gamebar__variant">{t(`variant.${game.variant}`)}</span>
          </div>
        </div>
        <div className="board">
          <div className="empty" style={{ marginTop: 'var(--space-2xl)' }}>
            <GridIcon size={36} className="empty__mark" />
            <p className="empty__title">{t('game.notBuilt')}</p>
            <p>{t('game.notBuiltBody')}</p>
            <button
              type="button"
              className="btn btn--primary"
              style={{ marginTop: 'var(--space-sm)' }}
              onClick={() => game.switchVariant('klondike')}
            >
              {variantIcon('klondike', 16)}
              {t('game.playKlondike')}
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="game">
      <div className="gamebar">
        <button type="button" className="iconbtn" onClick={onExit} aria-label={t('a11y.back')}>
          <ArrowLeftIcon size={20} />
        </button>
        <div className="gamebar__meta">
          <span className="gamebar__variant">{t(`variant.${game.variant}`)}</span>
          <span className="gamebar__sub">
            {t('game.level', { n: game.level })} · {t(`difficulty.${game.grade}`)} · {game.score}
          </span>
        </div>

        <button
          type="button"
          className="iconbtn"
          onClick={game.undo}
          disabled={!game.canUndo}
          aria-label={t('game.undo')}
        >
          <UndoIcon size={20} />
        </button>
        <button
          type="button"
          className="iconbtn"
          onClick={game.requestHint}
          disabled={game.hintsRemaining === 0}
          aria-label={t('game.hint')}
        >
          <BulbIcon size={20} />
          <span className="gamebar__hint-count">{game.hintsRemaining}</span>
        </button>
        <button
          type="button"
          className="iconbtn"
          onClick={game.newDeal}
          aria-label={t('game.newDeal')}
        >
          <RefreshIcon size={20} />
        </button>
        <button
          type="button"
          className="iconbtn"
          onClick={() => setMenuOpen(true)}
          aria-label={t('game.menu')}
        >
          <GridIcon size={20} />
        </button>
      </div>

      <Board key={game.dealNonce} game={game} />

      {game.won ? (
        <WinCascade
          onNext={game.newDeal}
          onMenu={onExit}
          onLeaderboard={() => setShowLeaderboard(true)}
          rank={rank}
          saveFailed={saveFailed}
          signedIn={user !== null}
          onSignIn={() => setShowAuth(true)}
        />
      ) : null}

      {showLeaderboard ? <LeaderboardModal onClose={() => setShowLeaderboard(false)} /> : null}
      {showAuth ? <AuthModal onClose={() => setShowAuth(false)} /> : null}

      {menuOpen ? (
        <div className="sheet" onClick={() => setMenuOpen(false)} role="presentation">
          <div
            className="sheet__panel"
            role="dialog"
            aria-label={t('game.switchTitle')}
            onClick={(e) => e.stopPropagation()}
          >
            <span className="sheet__title">{t('game.switchTitle')}</span>
            {PLAYABLE.map((variant) => (
              <button
                key={variant}
                type="button"
                className={
                  variant === game.variant ? 'btn btn--primary btn--block' : 'btn btn--block'
                }
                onClick={() => {
                  if (variant !== game.variant) {
                    game.switchVariant(variant);
                  }
                  setMenuOpen(false);
                }}
              >
                {variantIcon(variant, 16)}
                {t(`variant.${variant}`)}
              </button>
            ))}
            <button
              type="button"
              className="btn btn--block"
              onClick={() => {
                game.newDeal();
                setMenuOpen(false);
              }}
            >
              <RefreshIcon size={16} />
              {t('game.newDeal')}
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
