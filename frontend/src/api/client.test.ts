import { describe, expect, it } from 'vitest';
import { ApiError, isRetryable, toApiError } from './client';

describe('toApiError', () => {
  it('maps a ValidationProblem to field errors with a representative message', () => {
    const error = toApiError(400, {
      title: 'One or more validation errors occurred.',
      errors: { Email: ['Enter a valid email address.'], Password: ['Too short.'] },
    });
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.fieldErrors).toEqual({
      Email: ['Enter a valid email address.'],
      Password: ['Too short.'],
    });
    // The message is the first field's first message, for a banner fallback.
    expect(error.message).toBe('Enter a valid email address.');
  });

  it('maps a Problem (title only) to the message', () => {
    const error = toApiError(401, { title: 'Invalid credentials.' });
    expect(error.status).toBe(401);
    expect(error.message).toBe('Invalid credentials.');
    expect(error.fieldErrors).toBeUndefined();
  });

  it('maps an { error } body (conflict / unprocessable) to the message', () => {
    const error = toApiError(409, { error: 'Already recorded.' });
    expect(error.message).toBe('Already recorded.');
  });

  it('falls back to a rate-limit message for 429 with no body', () => {
    const error = toApiError(429, null);
    expect(error.message).toMatch(/too many/i);
  });

  it('falls back to an offline message for a synthetic status 0', () => {
    const error = toApiError(0, null);
    expect(error.message).toMatch(/reach the server/i);
  });
});

describe('isRetryable', () => {
  it('retries transient failures (unreachable, timeout, throttled, server error)', () => {
    // A sleeping free-tier backend surfaces as 0 (unreachable) or a 5xx.
    expect(isRetryable(new ApiError(0, 'offline'))).toBe(true);
    expect(isRetryable(new ApiError(408, 'timeout'))).toBe(true);
    expect(isRetryable(new ApiError(429, 'slow down'))).toBe(true);
    expect(isRetryable(new ApiError(500, 'boom'))).toBe(true);
    expect(isRetryable(new ApiError(502, 'bad gateway'))).toBe(true);
  });

  it('does not retry definitive answers — retrying cannot change them', () => {
    expect(isRetryable(new ApiError(400, 'validation'))).toBe(false);
    expect(isRetryable(new ApiError(401, 'unauthenticated'))).toBe(false);
    expect(isRetryable(new ApiError(409, 'duplicate game'))).toBe(false);
    // 422 = the anti-cheat rejected the submission; never hammer it.
    expect(isRetryable(new ApiError(422, 'failed verification'))).toBe(false);
  });

  it('does not retry non-ApiError throwables', () => {
    expect(isRetryable(new Error('bug'))).toBe(false);
  });
});
