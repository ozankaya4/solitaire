import { useTranslation } from 'react-i18next';
import { ArrowLeftIcon } from '../icons/icons';

/** Privacy / Terms page. Content comes fully from the locale resources. */
export function LegalScreen({ page, onBack }: { page: 'privacy' | 'terms'; onBack: () => void }) {
  const { t } = useTranslation();
  const paragraphs = ['p1', 'p2', 'p3', 'p4'] as const;

  return (
    <section className="screen" aria-labelledby={`${page}-title`}>
      <div className="screen__head">
        <button type="button" className="iconbtn" onClick={onBack} aria-label={t('a11y.back')}>
          <ArrowLeftIcon size={20} />
        </button>
        <h2 className="screen__heading" id={`${page}-title`}>
          {t(`${page}.title`)}
        </h2>
      </div>

      <div className="panel legal">
        {paragraphs.map((key) => (
          <p key={key} className="legal__para">
            {t(`${page}.${key}`)}
          </p>
        ))}
      </div>
    </section>
  );
}
