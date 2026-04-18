import { Injectable } from '@angular/core';

/**
 * AQ-370 — lightweight network check used before operations that need the server
 * (e.g. starting a round, transmitting a mandate). Pings /api/health with a short
 * timeout and returns a boolean — no retries, caller decides what to do.
 */
@Injectable({ providedIn: 'root' })
export class NetworkCheckService {
  async pingServer(timeoutMs = 3000): Promise<boolean> {
    if (!navigator.onLine) {
      return false;
    }

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), timeoutMs);

    try {
      const response = await fetch('/api/health', {
        method: 'GET',
        signal: controller.signal,
        cache: 'no-store',
        credentials: 'include',
      });
      return response.ok;
    } catch {
      return false;
    } finally {
      clearTimeout(timeout);
    }
  }
}
