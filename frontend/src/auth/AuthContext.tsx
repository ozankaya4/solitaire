// Authentication state for the SPA. Boots by probing /me (the cookie session is
// the source of truth), and exposes register/login/logout. Login is optional:
// guests play fully offline, and only signing in enables leaderboard submission.

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import type { LoginRequest, RegisterRequest, UserResponse } from '../api/types';

interface AuthContextValue {
  /** The signed-in user, or null when anonymous. */
  readonly user: UserResponse | null;
  /** True until the initial /me probe resolves. */
  readonly loading: boolean;
  register: (input: RegisterRequest) => Promise<void>;
  login: (input: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [loading, setLoading] = useState(true);

  // Restore the session on first mount. A network failure just leaves the app in
  // its guest state — the game itself never depends on the server.
  useEffect(() => {
    let active = true;
    void api
      .me()
      .then((me) => {
        if (active) {
          setUser(me);
        }
      })
      .catch(() => undefined)
      .finally(() => {
        if (active) {
          setLoading(false);
        }
      });
    return () => {
      active = false;
    };
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      register: async (input) => {
        setUser(await api.register(input));
      },
      login: async (input) => {
        setUser(await api.login(input));
      },
      logout: async () => {
        // Best-effort: clear the UI even if the network call fails; a stale cookie
        // resolves itself on the next /me probe.
        try {
          await api.logout();
        } catch {
          /* ignore */
        }
        setUser(null);
      },
    }),
    [user, loading],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (value === null) {
    throw new Error('useAuth must be used within an AuthProvider.');
  }
  return value;
}
