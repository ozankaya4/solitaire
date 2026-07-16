// A centered modal dialog: backdrop, Escape-to-close, focus trap on open, and a
// reduced-motion-aware entrance. Used for the auth and leaderboard pop-ups.

import { useEffect, useRef, type ReactNode } from 'react';
import { motion, useReducedMotion } from 'motion/react';
import { useTranslation } from 'react-i18next';
import { CloseIcon } from '../icons/icons';

interface ModalProps {
  readonly title: string;
  readonly onClose: () => void;
  readonly children: ReactNode;
}

export function Modal({ title, onClose, children }: ModalProps) {
  const reduce = useReducedMotion() ?? false;
  const { t } = useTranslation();
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onKey = (event: KeyboardEvent): void => {
      if (event.key === 'Escape') {
        onClose();
      }
    };
    document.addEventListener('keydown', onKey);
    // Move focus into the dialog for keyboard + screen-reader users.
    panelRef.current?.focus();
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  return (
    <div className="modal" role="presentation" onClick={onClose}>
      <motion.div
        ref={panelRef}
        className="modal__panel"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
        initial={reduce ? false : { opacity: 0, scale: 0.96, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        transition={reduce ? { duration: 0 } : { duration: 0.24, ease: [0.16, 0.9, 0.1, 1] }}
      >
        <div className="modal__head">
          <h2 className="modal__title">{title}</h2>
          <button
            type="button"
            className="iconbtn"
            onClick={onClose}
            aria-label={t('a11y.close')}
          >
            <CloseIcon size={20} />
          </button>
        </div>
        {children}
      </motion.div>
    </div>
  );
}
