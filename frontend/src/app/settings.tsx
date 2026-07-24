// Global player settings (theme, default variant, language) persisted to
// localStorage and applied to the document (data-theme, lang, i18n).

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import i18n from '../i18n';
import { getPersistedSettings, setPersistedSettings } from '../storage/cache';
import type { Language, ThemeName, VariantId } from './types';

interface Settings {
  readonly theme: ThemeName;
  readonly defaultVariant: VariantId;
  readonly language: Language;
}

interface SettingsContextValue extends Settings {
  setTheme: (theme: ThemeName) => void;
  toggleTheme: () => void;
  setDefaultVariant: (variant: VariantId) => void;
  setLanguage: (language: Language) => void;
}

const SettingsContext = createContext<SettingsContextValue | null>(null);

function prefersLight(): boolean {
  return (
    typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: light)').matches
  );
}

function loadInitial(): Settings {
  const base: Settings = {
    theme: prefersLight() ? 'light' : 'dark',
    defaultVariant: 'klondike',
    language: 'en',
  };
  // The cache is hydrated from IndexedDB before render (see main.tsx bootstrap).
  return { ...base, ...(getPersistedSettings() ?? {}) };
}

export function SettingsProvider({ children }: { children: ReactNode }) {
  const [settings, setSettings] = useState<Settings>(loadInitial);

  // Apply + persist whenever settings change.
  useEffect(() => {
    document.documentElement.dataset.theme = settings.theme;
    document.documentElement.lang = settings.language;
    if (i18n.language !== settings.language) {
      void i18n.changeLanguage(settings.language);
    }
    setPersistedSettings(settings);
  }, [settings]);

  const value = useMemo<SettingsContextValue>(
    () => ({
      ...settings,
      setTheme: (theme) => setSettings((s) => ({ ...s, theme })),
      toggleTheme: () =>
        setSettings((s) => ({ ...s, theme: s.theme === 'dark' ? 'light' : 'dark' })),
      setDefaultVariant: (defaultVariant) => setSettings((s) => ({ ...s, defaultVariant })),
      setLanguage: (language) => setSettings((s) => ({ ...s, language })),
    }),
    [settings],
  );

  return <SettingsContext value={value}>{children}</SettingsContext>;
}

export function useSettings(): SettingsContextValue {
  const value = useContext(SettingsContext);
  if (value === null) {
    throw new Error('useSettings must be used within a SettingsProvider.');
  }
  return value;
}
