import { useTranslation } from 'react-i18next';
import { ArrowLeftIcon, BookmarkStackIcon } from '../icons/icons';

export function SavedGamesScreen({ onBack }: { onBack: () => void }) {
  const { t } = useTranslation();

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

      <div className="empty">
        <BookmarkStackIcon size={40} className="empty__mark" />
        <p className="empty__title">{t('saved.emptyTitle')}</p>
        <p>{t('saved.emptyBody')}</p>
      </div>
    </section>
  );
}
