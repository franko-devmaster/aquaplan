import { environment } from '../../environments/environment';

/**
 * Sprint Sec (audit frontend F-031 / mission item 8) — diagnostic logging helper.
 * Console diagnostics (offline/sync/auth traces) are useful in development but must
 * never reach the production console: they expose internal ids, payloads, and auth
 * lifecycle details. `devInfo` is a no-op in production builds.
 */
export function devInfo(...args: unknown[]): void {
  if (!environment.production) {
    console.info(...args);
  }
}

/** Like devInfo, for warnings. No-op in production. */
export function devWarn(...args: unknown[]): void {
  if (!environment.production) {
    console.warn(...args);
  }
}

/** Like devInfo, for errors. No-op in production. */
export function devError(...args: unknown[]): void {
  if (!environment.production) {
    console.error(...args);
  }
}
