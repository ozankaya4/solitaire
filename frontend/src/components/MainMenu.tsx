import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useSettings } from '../app/settings';
import { useAuth } from '../auth/AuthContext';
import { VARIANTS } from '../app/variants';
import type { VariantId } from '../app/types';
import {
  BookmarkStackIcon,
  GearIcon,
  PlayIcon,
  StarIcon,
  TrophyIcon,
  UserIcon,
} from '../icons/icons';
import { Select, type SelectOption } from './Select';
import { variantIcon } from './variantIcon';

interface MainMenuProps {
  onPlay: (variant: VariantId) => void;
  onOpenSettings: () => void;
  onOpenSaved: () => void;
  onOpenLeaderboard: () => void;
  onOpenAuth: () => void;
}

export function MainMenu({
  onPlay,
  onOpenSettings,
  onOpenSaved,
  onOpenLeaderboard,
  onOpenAuth,
}: MainMenuProps) {
  const { t } = useTranslation();
  const { defaultVariant, drawMode } = useSettings();
  const { user, logout } = useAuth();
  // The picker here is a one-off "what to play right now" choice, seeded from
  // the persistent Settings default but never writing back to it — picking a
  // game on this screen must not silently change the app's default variant.
  const [selectedVariant, setSelectedVariant] = useState<VariantId>(defaultVariant);

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
          value={selectedVariant}
          options={options}
          onChange={(value) => setSelectedVariant(value as VariantId)}
        />
      </div>

      <button
        type="button"
        className="btn btn--primary btn--block"
        onClick={() => onPlay(selectedVariant)}
      >
        <PlayIcon size={18} />
        {t('menu.play')}
      </button>

      <button type="button" className="btn btn--block" onClick={onOpenLeaderboard}>
        <TrophyIcon size={18} />
        {t('menu.leaderboard')}
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

      {user ? (
        <div className="account">
          <span className="account__id">
            <UserIcon size={16} />
            {user.username}
          </span>
          <button type="button" className="account__link" onClick={() => void logout()}>
            {t('auth.logout')}
          </button>
        </div>
      ) : (
        <button type="button" className="btn btn--block" onClick={onOpenAuth}>
          <UserIcon size={18} />
          {t('auth.signIn')}
        </button>
      )}

      <p className="footnote">
        <StarIcon size={12} />{' '}
        {t('menu.drawNote', { count: drawMode, variant: t(`variant.${selectedVariant}`) })}
      </p>
    </section>
  );
}
