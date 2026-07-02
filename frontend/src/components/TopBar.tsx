import { useTranslation } from 'react-i18next';
import { useSettings } from '../app/settings';
import { CardsIcon, MoonIcon, SunIcon } from '../icons/icons';

export function TopBar({ onHome }: { onHome: () => void }) {
  const { t } = useTranslation();
  const { theme, toggleTheme } = useSettings();

  return (
    <header className="topbar">
      <button type="button" className="brand" onClick={onHome} aria-label={t('a11y.home')}>
        <span className="brand__mark">
          <CardsIcon size={20} />
        </span>
        <span className="brand__word">{t('brand')}</span>
      </button>

      <div className="topbar__actions">
        <button
          type="button"
          className="iconbtn"
          onClick={toggleTheme}
          aria-label={t('a11y.toggleTheme')}
          aria-pressed={theme === 'light'}
        >
          {theme === 'dark' ? <MoonIcon size={20} /> : <SunIcon size={20} />}
        </button>
      </div>
    </header>
  );
}
