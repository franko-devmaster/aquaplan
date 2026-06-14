import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateService } from '@ngx-translate/core';

/**
 * F-014 — single place to surface API/HTTP errors. Replaces ~15 hand-rolled
 * `catch { snackBar.open(apiError?.error?.error ?? 'Error', ...) }` blocks that
 * showed a non-i18n `'Error'` fallback. Extracts the backend message when
 * present, otherwise shows a translated generic message.
 */
@Injectable({ providedIn: 'root' })
export class ApiErrorService {
  private readonly snackBar = inject(MatSnackBar);
  private readonly translate = inject(TranslateService);

  /** Best-effort extraction of a human-readable message from an HTTP error. */
  extract(err: unknown, fallbackKey = 'errors.generic'): string {
    if (err && typeof err === 'object') {
      const maybe = err as { error?: { error?: string; message?: string } | string };
      if (typeof maybe.error === 'string' && maybe.error.trim()) {
        return maybe.error;
      }
      if (maybe.error && typeof maybe.error === 'object') {
        if (maybe.error.error?.trim()) return maybe.error.error;
        if (maybe.error.message?.trim()) return maybe.error.message;
      }
    }
    return this.translate.instant(fallbackKey);
  }

  /** Shows an error toast with the extracted message and a Close action. */
  toast(err: unknown, fallbackKey = 'errors.generic'): void {
    this.snackBar.open(
      this.extract(err, fallbackKey),
      this.translate.instant('common.close'),
      { duration: 5000 }
    );
  }

  /** Shows a translated success toast (shorter duration). */
  success(messageKey: string): void {
    this.snackBar.open(
      this.translate.instant(messageKey),
      this.translate.instant('common.close'),
      { duration: 3000 }
    );
  }
}
