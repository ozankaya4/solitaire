import { useTranslation } from 'react-i18next';
import { useSettings } from '../app/settings';
import { VARIANTS } from '../app/variants';
import type { VariantId } from '../app/types';
import { BookmarkStackIcon, GearIcon, PlayIcon, StarIcon } from '../icons/icons';
import { Select, type SelectOption } from './Select';
import { variantIcon } from './variantIcon';

interface MainMenuProps {
  onPlay: () => void;
  onOpenSettings: () => void;
  onOpenSaved: () => void;
}

export function MainMenu({ onPlay, onOpenSettings, onOpenSaved }: MainMenuProps) {
  const { t } = useTranslation();
  const { defaultVariant, setDefaultVariant, drawMode } = useSettings();

  const options: SelectOption[] = VARIANTS.map((variant) => ({
    value: variant.id,
    label: t(`variant.${variant.id}`),
    icon: variantIcon(variant.id),
    badge: variant.popular ? t('menu.mostPopular') : undefined,
  }));

  return (
    <section className="screen" aria-labelledby="menu-title">
      <div>
        <p className="kicker">{t('menu.kicker')}</p>
        <h1 className="title" id="menu-title">
          <span className="title__a">{t('menu.titleA')}</span>
          <span className="title__b">{t('menu.titleB')}</span>
        </h1>
      </div>

      <p className="tagline">{t('menu.tagline')}</p>

      <div className="panel panel--accent">
        <p className="panel__label">{t('menu.choose')}</p>
        <Select
          label={t('menu.choose')}
          value={defaultVariant}
          options={options}
          onChange={(value) => setDefaultVariant(value as VariantId)}
        />
      </div>

      <button type="button" className="btn btn--primary btn--block" onClick={onPlay}>
        <PlayIcon size={18} />
        {t('menu.play')}
      </button>

      <div className="btn__row">
        <button type="button" className="btn" onClick={onOpenSettings}>
          <GearIcon size={18} />
          {t('menu.settings')}
        </button>
        <button type="button" className="btn" onClick={onOpenSaved}>
          <BookmarkStackIcon size={18} />
          {t('menu.saved')}
        </button>
      </div>

      <p className="footnote">
        <StarIcon size={12} />{' '}
        {t('menu.drawNote', { count: drawMode, variant: t(`variant.${defaultVariant}`) })}
      </p>
    </section>
  );
}
