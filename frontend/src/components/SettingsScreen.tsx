import { useTranslation } from 'react-i18next';
import { useSettings } from '../app/settings';
import { VARIANTS } from '../app/variants';
import type { DrawMode, Language, ThemeName, VariantId } from '../app/types';
import { ArrowLeftIcon, GlobeIcon, MoonIcon, SunIcon } from '../icons/icons';
import { Segmented } from './Segmented';
import { Select, type SelectOption } from './Select';
import { variantIcon } from './variantIcon';

export function SettingsScreen({ onBack }: { onBack: () => void }) {
  const { t } = useTranslation();
  const {
    theme,
    setTheme,
    defaultVariant,
    setDefaultVariant,
    language,
    setLanguage,
    drawMode,
    setDrawMode,
  } = useSettings();

  const variantOptions: SelectOption[] = VARIANTS.map((variant) => ({
    value: variant.id,
    label: t(`variant.${variant.id}`),
    icon: variantIcon(variant.id),
    badge: variant.popular ? t('menu.mostPopular') : undefined,
  }));

  return (
    <section className="screen" aria-labelledby="settings-title">
      <div className="screen__head">
        <button type="button" className="iconbtn" onClick={onBack} aria-label={t('a11y.back')}>
          <ArrowLeftIcon size={20} />
        </button>
        <h2 className="screen__heading" id="settings-title">
          {t('settings.title')}
        </h2>
      </div>

      <div className="panel">
        <div className="field">
          <span className="field__label">{t('settings.defaultVariant')}</span>
          <span className="field__hint">{t('settings.defaultVariantHint')}</span>
          <Select
            label={t('settings.defaultVariant')}
            value={defaultVariant}
            options={variantOptions}
            onChange={(value) => setDefaultVariant(value as VariantId)}
          />
        </div>

        <div className="field">
          <span className="field__label">{t('settings.theme')}</span>
          <span className="field__hint">{t('settings.themeHint')}</span>
          <Segmented<ThemeName>
            block
            label={t('settings.theme')}
            value={theme}
            onChange={setTheme}
            options={[
              { value: 'dark', label: t('settings.dark'), icon: <MoonIcon size={16} /> },
              { value: 'light', label: t('settings.light'), icon: <SunIcon size={16} /> },
            ]}
          />
        </div>

        <div className="field">
          <span className="field__label">{t('settings.language')}</span>
          <span className="field__hint">{t('settings.languageHint')}</span>
          <Segmented<Language>
            block
            label={t('settings.language')}
            value={language}
            onChange={setLanguage}
            options={[
              { value: 'en', label: 'EN', icon: <GlobeIcon size={16} /> },
              { value: 'tr', label: 'TR', icon: <GlobeIcon size={16} /> },
            ]}
          />
        </div>

        <div className="field">
          <span className="field__label">{t('settings.draw')}</span>
          <span className="field__hint">{t('settings.drawHint')}</span>
          <Segmented<string>
            block
            label={t('settings.draw')}
            value={String(drawMode)}
            onChange={(value) => setDrawMode(Number(value) as DrawMode)}
            options={[
              { value: '1', label: t('settings.draw1') },
              { value: '3', label: t('settings.draw3') },
            ]}
          />
        </div>
      </div>
    </section>
  );
}
