import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

// Localization bootstrap. Translation resources are intentionally empty for now;
// namespaces and locale files will be added alongside the UI.
const resources = {
  en: {
    translation: {},
  },
} as const;

void i18n.use(initReactI18next).init({
  resources,
  lng: 'en',
  fallbackLng: 'en',
  interpolation: {
    // React already escapes values against XSS.
    escapeValue: false,
  },
});

export default i18n;
