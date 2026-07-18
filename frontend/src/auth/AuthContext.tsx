// Authentication state for the SPA. Boots by probing /me (the cookie session is
// the source of truth), and exposes register/login/logout. Login is optional:
// guests play fully offline, and only signing in enables leaderboard submission.

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, withRetry } from '../api/client';
import type { LoginRequest, RegisterRequest, UserResponse } from '../api/types';
import { startCloudSync, stopCloudSync } from '../storage/cloudSync';

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
  // its guest state — the game itself never depends on the server. This probe is
  // also the session's first API touch, so it doubles as the wake-up call for the
  // sleeping free-tier backend; the retry rides out the cold start.
  useEffect(() => {
    let active = true;
    void withRetry(() => api.me(), 3, 3000)
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

  // Mirror the game data to/from the account while signed in (cross-device sync).
  useEffect(() => {
    if (user) {
      startCloudSync(user.id);
    } else {
      stopCloudSync();
    }
  }, [user]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loading,
      // Retried: a cold-starting backend drops the first request of a session
      // with a phantom "could not reach" — riding it out beats surfacing it.
      // (If a lost-response register DID land, the retry returns "name taken"
      // and the player simply logs in.)
      register: async (input) => {
        setUser(await withRetry(() => api.register(input), 3, 3000));
      },
      login: async (input) => {
        setUser(await withRetry(() => api.login(input), 3, 3000));
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
