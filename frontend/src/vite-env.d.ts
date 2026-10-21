/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_ECO_API_MODE?: 'live' | 'mock';
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
