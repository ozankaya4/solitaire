import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

// Self-hosted fonts (bundled woff2, not a CDN): slab display + mono UI.
import '@fontsource/zilla-slab/400.css';
import '@fontsource/zilla-slab/600.css';
import '@fontsource/zilla-slab/700.css';
import '@fontsource/space-mono/400.css';
import '@fontsource/space-mono/700.css';

import './styles/tokens.css';
import './styles/base.css';
import './styles/components.css';
import './styles/board.css';

import './i18n';
import App from './App';
import { SettingsProvider } from './app/settings';
import { hydrateStore } from './storage/cache';

function registerServiceWorker(): void {
  if (!import.meta.env.PROD || !('serviceWorker' in navigator)) {
    return;
  }
  const register = (): void => {
    void navigator.serviceWorker.register('/sw.js').catch(() => undefined);
  };
  // bootstrap() awaits IndexedDB hydration, so `load` may have already fired by
  // now — register immediately in that case, otherwise wait for load.
  if (document.readyState === 'complete') {
    register();
  } else {
    window.addEventListener('load', register, { once: true });
  }
}

async function bootstrap(): Promise<void> {
  const rootElement = document.getElementById('root');
  if (!rootElement) {
    throw new Error('Root element #root not found');
  }

  // Hydrate persisted data from IndexedDB before first render so settings, the
  // current level, and saved games are available synchronously.
  await hydrateStore().catch(() => undefined);

  createRoot(rootElement).render(
    <StrictMode>
      <SettingsProvider>
        <App />
      </SettingsProvider>
    </StrictMode>,
  );

  registerServiceWorker();
}

void bootstrap();
