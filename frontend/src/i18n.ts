import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './locales/en.json';
import tr from './locales/tr.json';

// Reads the persisted language from the localStorage settings mirror so the very
// first render is already in the player's language (IndexedDB hydration keeps the
// mirror in sync; SettingsProvider drives changes from then on, without reload).
function initialLanguage(): string {
  try {
    const raw = localStorage.getItem('solitaire:settings');
    if (raw !== null) {
      const settings = JSON.parse(raw) as { language?: string };
      if (settings.language === 'tr' || settings.language === 'en') {
        return settings.language;
      }
    }
  } catch {
    /* fall through to default */
  }
  return 'en';
}

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    tr: { translation: tr },
  },
  lng: initialLanguage(),
  fallbackLng: 'en',
  interpolation: {
    // React already escapes values against XSS.
    escapeValue: false,
  },
});

export default i18n;
