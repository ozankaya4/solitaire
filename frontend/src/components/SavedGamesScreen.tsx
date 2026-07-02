import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { deleteSave, getStats, listSaves } from '../storage/cache';
import type { SavedGame } from '../storage/types';
import type { VariantId } from '../app/types';
import { ArrowLeftIcon, BookmarkStackIcon, PlayIcon } from '../icons/icons';
import { variantIcon } from './variantIcon';

function formatTime(ms: number | null): string {
  if (ms === null) {
    return '—';
  }
  const total = Math.round(ms / 1000);
  const m = Math.floor(total / 60);
  const s = total % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

export function SavedGamesScreen({
  onBack,
  onResume,
}: {
  onBack: () => void;
  onResume: (variant: VariantId) => void;
}) {
  const { t } = useTranslation();
  const [saves, setSaves] = useState<SavedGame[]>(() => listSaves());

  const remove = (variant: VariantId) => {
    deleteSave(variant);
    setSaves(listSaves());
  };

  return (
    <section className="screen" aria-labelledby="saved-title">
      <div className="screen__head">
        <button type="button" className="iconbtn" onClick={onBack} aria-label={t('a11y.back')}>
          <ArrowLeftIcon size={20} />
        </button>
        <h2 className="screen__heading" id="saved-title">
          {t('saved.title')}
        </h2>
      </div>

      {saves.length === 0 ? (
        <div className="empty">
          <BookmarkStackIcon size={40} className="empty__mark" />
          <p className="empty__title">{t('saved.emptyTitle')}</p>
          <p>{t('saved.emptyBody')}</p>
        </div>
      ) : (
        <ul className="savelist">
          {saves.map((save) => {
            const stats = getStats(save.variant);
            const rate =
              stats.gamesPlayed === 0 ? 0 : Math.round((stats.wins / stats.gamesPlayed) * 100);
            return (
              <li key={save.variant} className="panel savecard">
                <div className="savecard__head">
                  <span className="savecard__icon">{variantIcon(save.variant, 20)}</span>
                  <div className="savecard__meta">
                    <span className="savecard__name">{t(`variant.${save.variant}`)}</span>
                    <span className="savecard__sub">
                      {t('game.level', { n: save.level })} ·{' '}
                      {t('saved.moves', { count: save.moves.length })}
                    </span>
                  </div>
                </div>

                <dl className="stats">
                  <div>
                    <dt>{t('saved.played')}</dt>
                    <dd>{stats.gamesPlayed}</dd>
                  </div>
                  <div>
                    <dt>{t('saved.wins')}</dt>
                    <dd>{stats.wins}</dd>
                  </div>
                  <div>
                    <dt>{t('saved.winRate')}</dt>
                    <dd>{rate}%</dd>
                  </div>
                  <div>
                    <dt>{t('saved.best')}</dt>
                    <dd>{formatTime(stats.bestTimeMs)}</dd>
                  </div>
                </dl>

                <div className="btn__row">
                  <button
                    type="button"
                    className="btn btn--primary"
                    onClick={() => onResume(save.variant)}
                  >
                    <PlayIcon size={16} />
                    {t('saved.resume')}
                  </button>
                  <button type="button" className="btn" onClick={() => remove(save.variant)}>
                    {t('saved.delete')}
                  </button>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
