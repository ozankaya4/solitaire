// Sign in / create account pop-up. Sign-up collects name (the public handle),
// email and password; login takes email + password. Validation mirrors the
// server rules (Auth/Contracts.cs) for instant feedback; the server remains the
// authority and its localized errors are surfaced verbatim.

import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { Segmented } from './Segmented';
import { Modal } from './Modal';

type Tab = 'login' | 'register';

// Matches RegisterRequest's [RegularExpression] and [StringLength(3,32)].
const USERNAME_RE = /^[a-zA-Z0-9_.-]+$/;
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function AuthModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const { register, login } = useAuth();
  const [tab, setTab] = useState<Tab>('login');

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const switchTab = (next: Tab): void => {
    setTab(next);
    setError(null);
    setFieldErrors({});
  };

  const validate = (): Record<string, string> => {
    const errors: Record<string, string> = {};
    if (tab === 'register') {
      if (name.length < 3 || name.length > 32 || !USERNAME_RE.test(name)) {
        errors.name = t('auth.error.name');
      }
    }
    if (!EMAIL_RE.test(email)) {
      errors.email = t('auth.error.email');
    }
    if (password.length < 8) {
      errors.password = t('auth.error.password');
    }
    return errors;
  };

  const onSubmit = async (event: FormEvent): Promise<void> => {
    event.preventDefault();
    const localErrors = validate();
    setFieldErrors(localErrors);
    setError(null);
    if (Object.keys(localErrors).length > 0) {
      return;
    }

    setPending(true);
    try {
      if (tab === 'register') {
        await register({ username: name, email, password });
      } else {
        await login({ usernameOrEmail: email, password, rememberMe: true });
      }
      onClose();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
        setFieldErrors(mapServerFields(err.fieldErrors));
      } else {
        setError(t('auth.error.generic'));
      }
    } finally {
      setPending(false);
    }
  };

  const title = tab === 'login' ? t('auth.loginTitle') : t('auth.registerTitle');

  return (
    <Modal title={title} onClose={onClose}>
      <Segmented<Tab>
        block
        label={t('auth.tabsLabel')}
        value={tab}
        onChange={switchTab}
        options={[
          { value: 'login', label: t('auth.login') },
          { value: 'register', label: t('auth.register') },
        ]}
      />

      <form className="form" onSubmit={(event) => void onSubmit(event)} noValidate>
        {tab === 'register' ? (
          <Field
            id="auth-name"
            label={t('auth.name')}
            hint={t('auth.nameHint')}
            value={name}
            onChange={setName}
            autoComplete="username"
            error={fieldErrors.name}
          />
        ) : null}

        <Field
          id="auth-email"
          label={t('auth.email')}
          type="email"
          value={email}
          onChange={setEmail}
          autoComplete="email"
          error={fieldErrors.email}
        />

        <Field
          id="auth-password"
          label={t('auth.password')}
          type="password"
          value={password}
          onChange={setPassword}
          autoComplete={tab === 'register' ? 'new-password' : 'current-password'}
          hint={tab === 'register' ? t('auth.passwordHint') : undefined}
          error={fieldErrors.password}
        />

        {error ? (
          <p className="form__error" role="alert">
            {error}
          </p>
        ) : null}

        {tab === 'register' ? <p className="form__note">{t('register.acknowledge')}</p> : null}

        <button type="submit" className="btn btn--primary btn--block" disabled={pending}>
          {pending
            ? t('auth.working')
            : tab === 'login'
              ? t('auth.login')
              : t('auth.createAccount')}
        </button>
      </form>
    </Modal>
  );
}

interface FieldProps {
  readonly id: string;
  readonly label: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
  readonly type?: string;
  readonly hint?: string;
  readonly error?: string;
  readonly autoComplete?: string;
}

function Field({ id, label, value, onChange, type = 'text', hint, error, autoComplete }: FieldProps) {
  return (
    <div className="field">
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      {hint ? <span className="field__hint">{hint}</span> : null}
      <input
        id={id}
        className="input"
        type={type}
        value={value}
        autoComplete={autoComplete}
        aria-invalid={error !== undefined}
        onChange={(event) => onChange(event.target.value)}
      />
      {error ? (
        <span className="form__error" role="alert">
          {error}
        </span>
      ) : null}
    </div>
  );
}

// Server validation keys are member names ("Email"/"Password"/"Username") or
// Identity error codes. Map the ones we can onto our fields; the rest surface in
// the general error banner (err.message).
function mapServerFields(fieldErrors?: Readonly<Record<string, string[]>>): Record<string, string> {
  if (!fieldErrors) {
    return {};
  }
  const mapped: Record<string, string> = {};
  for (const [key, messages] of Object.entries(fieldErrors)) {
    const message = messages[0];
    if (!message) {
      continue;
    }
    const lower = key.toLowerCase();
    if (lower.includes('username') || lower === 'name') {
      mapped.name = message;
    } else if (lower.includes('email')) {
      mapped.email = message;
    } else if (lower.includes('password')) {
      mapped.password = message;
    }
  }
  return mapped;
}
