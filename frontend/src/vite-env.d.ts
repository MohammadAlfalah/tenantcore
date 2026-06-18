/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Optional API base URL. Empty by default; relative `/api/*` calls are proxied to the backend. */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
