// Global leaderboard pop-up. Ranks players by the highest level they have a
// server-verified win for, per variant. Reads are public; when signed in, the
// player's own row is highlighted and their rank is summarized.

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, withRetry } from '../api/client';
import type { LeaderboardResponse } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import type { VariantId } from '../app/types';
import { Modal } from './Modal';
import { Segmented } from './Segmented';
import { variantIcon } from './variantIcon';

const RANKED: readonly VariantId[] = ['klondike', 'spider', 'freecell', 'pyramid', 'tripeaks'];

function formatTime(ms: number): string {
  const total = Math.round(ms / 1000);
  const m = Math.floor(total / 60);
  const s = total % 60;
  return `${m}:${String(s).padStart(2, '0')}`;
}

export function LeaderboardModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [variant, setVariant] = useState<VariantId>('klondike');
  const [board, setBoard] = useState<LeaderboardResponse | null>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading');

  useEffect(() => {
    let active = true;
    setStatus('loading');
    setBoard(null);
    // Retried: the first request of a session may hit the free backend's cold start.
    withRetry(() => api.leaderboard(variant, 20), 3, 3000)
      .then((data) => {
        if (active) {
          setBoard(data);
          setStatus('ready');
        }
      })
      .catch(() => {
        if (active) {
          setStatus('error');
        }
      });
    return () => {
      active = false;
    };
  }, [variant]);

  return (
    <Modal title={t('leaderboard.title')} onClose={onClose}>
      <Segmented<VariantId>
        block
        label={t('leaderboard.variantLabel')}
        value={variant}
        onChange={setVariant}
        options={RANKED.map((id) => ({
          value: id,
          label: t(`variant.${id}`),
          icon: variantIcon(id, 16),
        }))}
      />

      {status === 'loading' ? <p className="lboard__msg">{t('leaderboard.loading')}</p> : null}
      {status === 'error' ? <p className="lboard__msg">{t('leaderboard.error')}</p> : null}

      {status === 'ready' && board ? (
        board.top.length === 0 ? (
          <p className="lboard__msg">{t('leaderboard.empty')}</p>
        ) : (
          <>
            <ol className="lboard" aria-label={t('leaderboard.title')}>
              <li className="lboard__row lboard__row--head" aria-hidden="true">
                <span className="lboard__rank">#</span>
                <span className="lboard__name">{t('leaderboard.player')}</span>
                <span className="lboard__level">{t('leaderboard.levelCol')}</span>
                <span className="lboard__time">{t('leaderboard.timeCol')}</span>
              </li>
              {board.top.map((row) => {
                const you = user?.username != null && row.username === user.username;
                return (
                  <li
                    key={`${row.rank}-${row.username}`}
                    className={you ? 'lboard__row is-you' : 'lboard__row'}
                  >
                    <span className="lboard__rank">{row.rank}</span>
                    <span className="lboard__name">{row.username}</span>
                    <span className="lboard__level">{row.level}</span>
                    <span className="lboard__time">{formatTime(row.timeMs)}</span>
                  </li>
                );
              })}
            </ol>

            <p className="lboard__foot">
              {board.playerRank != null && board.playerBestLevel != null
                ? t('leaderboard.yourRank', {
                    rank: board.playerRank,
                    level: board.playerBestLevel,
                  })
                : user
                  ? t('leaderboard.unranked')
                  : t('leaderboard.signedOut')}
            </p>
          </>
        )
      ) : null}
    </Modal>
  );
}
