// Hand-built accessible dropdown (no native <select>, no component library).
// Keyboard: Enter/Space/ArrowDown open; Arrow keys move; Enter selects; Esc closes.

import { useEffect, useRef, useState, type ReactNode } from 'react';
import { ChevronDownIcon, CheckIcon } from '../icons/icons';

export interface SelectOption {
  readonly value: string;
  readonly label: string;
  readonly badge?: string;
  readonly icon?: ReactNode;
}

interface SelectProps {
  readonly value: string;
  readonly options: readonly SelectOption[];
  readonly onChange: (value: string) => void;
  readonly label: string;
}

export function Select({ value, options, onChange, label }: SelectProps) {
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);

  const selectedIndex = Math.max(
    0,
    options.findIndex((o) => o.value === value),
  );
  const selected = options[selectedIndex] ?? options[0];

  useEffect(() => {
    if (!open) {
      return;
    }
    setActiveIndex(selectedIndex);
    const onPointerDown = (event: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', onPointerDown);
    return () => document.removeEventListener('mousedown', onPointerDown);
  }, [open, selectedIndex]);

  const commit = (index: number): void => {
    const option = options[index];
    if (option) {
      onChange(option.value);
    }
    setOpen(false);
  };

  const onFieldKeyDown = (event: React.KeyboardEvent): void => {
    if (
      event.key === 'ArrowDown' ||
      event.key === 'ArrowUp' ||
      event.key === 'Enter' ||
      event.key === ' '
    ) {
      event.preventDefault();
      setOpen(true);
    }
  };

  const onMenuKeyDown = (event: React.KeyboardEvent): void => {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActiveIndex((i) => Math.min(options.length - 1, i + 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActiveIndex((i) => Math.max(0, i - 1));
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      commit(activeIndex);
    } else if (event.key === 'Escape') {
      setOpen(false);
    }
  };

  if (selected === undefined) {
    return null;
  }

  return (
    <div className="select" ref={rootRef}>
      <button
        type="button"
        className="select__field"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={label}
        onClick={() => setOpen((o) => !o)}
        onKeyDown={onFieldKeyDown}
      >
        <span className="select__value">
          {selected.icon}
          <span className="select__name">{selected.label}</span>
          {selected.badge ? <span className="badge">{selected.badge}</span> : null}
        </span>
        <ChevronDownIcon size={18} className="select__chevron" />
      </button>

      {open ? (
        <ul
          className="select__menu"
          role="listbox"
          aria-label={label}
          onKeyDown={onMenuKeyDown}
          tabIndex={-1}
        >
          {options.map((option, index) => (
            <li key={option.value}>
              <button
                type="button"
                className="option"
                role="option"
                aria-selected={option.value === value}
                data-active={index === activeIndex}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => commit(index)}
              >
                {option.icon}
                <span className="option__label">{option.label}</span>
                {option.badge ? <span className="badge">{option.badge}</span> : null}
                <CheckIcon size={16} className="option__check" />
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
