// Thin, typed wrapper over the Solitaire API. Responsibilities:
//  - always send the auth cookie (credentials: 'include');
//  - attach the anti-forgery token to state-changing requests (double-submit);
//  - ask the server for messages in the user's language (Accept-Language);
//  - normalize every failure into a single ApiError the UI can render.
//
// The backend already localizes validation/error messages, so we surface them
// as-is rather than re-translating on the client.

import type {
  LeaderboardResponse,
  LoginRequest,
  RegisterRequest,
  SubmitGameRequest,
  SubmitGameResponse,
  UserResponse,
} from './types';

// Empty in production (same-origin); a separate origin in dev (see .env.development).
const API_BASE = import.meta.env.VITE_API_URL ?? '';

/** A normalized API failure. `fieldErrors` maps a form field to its messages. */
export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors?: Readonly<Record<string, string[]>>;

  constructor(status: number, message: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

interface ProblemBody {
  readonly title?: string;
  readonly error?: string;
  readonly errors?: Record<string, string[]>;
}

/**
 * Turns an error response body into an ApiError. Pure and exported so the shape
 * mapping can be unit-tested without a network. Handles the three server shapes:
 * ValidationProblem (`errors`), Problem (`title`), and `{ error }`.
 */
export function toApiError(status: number, body: unknown): ApiError {
  const problem = (body ?? {}) as ProblemBody;
  if (problem.errors && Object.keys(problem.errors).length > 0) {
    const first = Object.values(problem.errors)[0]?.[0];
    return new ApiError(status, first ?? problem.title ?? 'Request failed.', problem.errors);
  }
  const message = problem.title ?? problem.error ?? defaultMessage(status);
  return new ApiError(status, message);
}

function defaultMessage(status: number): string {
  if (status === 429) {
    return 'Too many attempts. Please wait a moment and try again.';
  }
  if (status === 0) {
    return 'Could not reach the server.';
  }
  return 'Request failed.';
}

function currentLanguage(): string {
  if (typeof document !== 'undefined' && document.documentElement.lang) {
    return document.documentElement.lang;
  }
  return 'en';
}

// -- anti-forgery -------------------------------------------------------------
// Fetched fresh each time it is needed (which also (re)sets the double-submit
// cookie). NOT cached: the server binds the token to the current identity, so a
// token minted in one session would fail validation after a different login.
async function getCsrfToken(): Promise<string> {
  const res = await fetch(`${API_BASE}/api/auth/csrf`, {
    credentials: 'include',
    headers: { 'Accept-Language': currentLanguage() },
  });
  const body = (await res.json()) as { token?: string };
  return body.token ?? '';
}

interface RequestOptions {
  readonly method?: 'GET' | 'POST';
  readonly body?: unknown;
  /** Attach the anti-forgery header (required for state-changing, cookie-authed calls). */
  readonly csrf?: boolean;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, csrf = false } = options;
  const headers: Record<string, string> = { 'Accept-Language': currentLanguage() };
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (csrf) {
    headers['X-CSRF-TOKEN'] = await getCsrfToken();
  }

  let res: Response;
  try {
    res = await fetch(`${API_BASE}${path}`, {
      method,
      credentials: 'include',
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0, defaultMessage(0));
  }

  const payload = await readBody(res);
  if (!res.ok) {
    throw toApiError(res.status, payload);
  }
  return payload as T;
}

async function readBody(res: Response): Promise<unknown> {
  const text = await res.text();
  if (!text) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return { error: text };
  }
}

/**
 * True for failures worth retrying: the network was unreachable, the request
 * timed out, or the server erred. A definitive answer (validation, auth, or an
 * anti-cheat verdict) is never retried — retrying would not change it.
 */
export function isRetryable(error: unknown): boolean {
  if (!(error instanceof ApiError)) {
    return false;
  }
  return error.status === 0 || error.status >= 500 || error.status === 408 || error.status === 429;
}

export const api = {
  register: (input: RegisterRequest) =>
    request<UserResponse>('/api/auth/register', { method: 'POST', body: input }),

  login: (input: LoginRequest) =>
    request<UserResponse>('/api/auth/login', { method: 'POST', body: input }),

  async logout(): Promise<void> {
    await request<void>('/api/auth/logout', { method: 'POST', csrf: true });
  },

  /** The signed-in user, or null when the session is anonymous. */
  async me(): Promise<UserResponse | null> {
    try {
      return await request<UserResponse>('/api/auth/me');
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        return null;
      }
      throw error;
    }
  },

  submitGame: (input: SubmitGameRequest) =>
    request<SubmitGameResponse>('/api/games/submit', { method: 'POST', body: input }),

  leaderboard: (variant: string, top = 20) =>
    request<LeaderboardResponse>(`/api/leaderboard/${variant}?top=${top}`),
};

/**
 * Submits a won game, retrying transient failures with a growing delay. The
 * backend may be asleep (free hosting cold-starts), so the first attempt after
 * an idle period can time out; losing a hard-won level to that would be cruel.
 */
export async function submitGameWithRetry(
  input: SubmitGameRequest,
  attempts = 3,
  delayMs = 2000,
): Promise<SubmitGameResponse> {
  for (let attempt = 1; ; attempt++) {
    try {
      return await api.submitGame(input);
    } catch (error) {
      if (attempt >= attempts || !isRetryable(error)) {
        throw error;
      }
      await new Promise((resolve) => setTimeout(resolve, delayMs * attempt));
    }
  }
}
