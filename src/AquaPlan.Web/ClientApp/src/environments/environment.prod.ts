// AQ-416 — version is stamped at build time by scripts/generate-version.mjs.
import { VERSION } from './version';

export const environment = {
  production: true,
  version: VERSION.version,
  commit: VERSION.commit,
  author: 'François Charrière',
  pwaEnabled: true,
};
