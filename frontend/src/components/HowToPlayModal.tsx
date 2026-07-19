// "How to play" pop-up: a step-by-step rules guide for the variant currently
// on the table. Copy lives in the locale files (howto.<variant>.steps), so
// each language carries its own full ruleset.

import { useTranslation } from 'react-i18next';
import type { VariantId } from '../app/types';
import { Modal } from './Modal';
import { variantIcon } from './variantIcon';

export function HowToPlayModal({
  variant,
  onClose,
}: {
  variant: VariantId;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const raw: unknown = t(`howto.${variant}.steps`, { returnObjects: true, defaultValue: '' });
  const steps = Array.isArray(raw) ? (raw as string[]) : [];

  return (
    <Modal title={t('howto.title', { variant: t(`variant.${variant}`) })} onClose={onClose}>
      <p className="howto__goal">
        {variantIcon(variant, 16)}
        <span>
          <strong>{t('howto.goalLabel')}: </strong>
          {t(`howto.${variant}.goal`)}
        </span>
      </p>
      {/* role="list" restores list semantics that Safari drops with list-style: none */}
      <ol className="howto" role="list">
        {steps.map((step, index) => (
          <li key={index} className="howto__step">
            {step}
          </li>
        ))}
      </ol>
    </Modal>
  );
}
