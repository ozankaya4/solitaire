/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL of the Solitaire API. Empty/undefined => same-origin (production). */
  readonly VITE_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
