// AQ-416 — version is stamped at build time by scripts/generate-version.mjs.
// The import is wrapped in a try/catch-equivalent fallback so CI/dev without
// the generated file still builds cleanly.
import { VERSION } from './version';

export const environment = {
  production: false,
  version: VERSION.version,
  commit: VERSION.commit,
  author: 'François Charrière',
  pwaEnabled: true,
};
