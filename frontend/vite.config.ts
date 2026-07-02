/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Allow the test suite to import the shared cross-language vectors from
    // ../shared, which lives above the frontend project root.
    fs: {
      allow: ['..'],
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
