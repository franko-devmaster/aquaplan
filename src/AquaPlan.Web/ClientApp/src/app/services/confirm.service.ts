import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { ConfirmDialogComponent } from '../components/confirm-dialog.component';
import { PromptDialogComponent, PromptDialogData } from '../components/prompt-dialog.component';

export interface ConfirmOptions {
  /** i18n key for the dialog title. Defaults to `common.confirm`. */
  titleKey?: string;
}

export interface PromptOptions {
  /** i18n key for the dialog title. Defaults to the label key. */
  titleKey?: string;
  /** Initial value of the input. */
  value?: string;
  /** Multi-line textarea instead of a single-line input. */
  multiline?: boolean;
  /** When true, an empty value cannot be confirmed. */
  required?: boolean;
}

/**
 * F-011 — single entry point for confirmation / prompt dialogs, replacing the
 * native `confirm()` / `prompt()` (unstyled, not themeable, inconsistent on iOS
 * PWA standalone). Wraps the Material `ConfirmDialogComponent` and
 * `PromptDialogComponent` behind a promise-based API so callers keep their
 * existing `if (!await ...) return;` control flow.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);
  private readonly translate = inject(TranslateService);

  /**
   * Opens a confirmation dialog. `messageKey` is an i18n key resolved here.
   * Resolves to true when the user confirms, false otherwise.
   */
  async confirm(messageKey: string, options: ConfirmOptions = {}): Promise<boolean> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      panelClass: 'responsive-dialog',
      data: {
        title: this.translate.instant(options.titleKey ?? 'common.confirm'),
        message: this.translate.instant(messageKey),
      },
    });
    return (await firstValueFrom(ref.afterClosed())) === true;
  }

  /**
   * Opens a text-input dialog. `labelKey` is an i18n key resolved here.
   * Resolves to the entered string on confirm, or `undefined` when cancelled
   * (mirrors the native `prompt()` null contract).
   */
  async prompt(labelKey: string, options: PromptOptions = {}): Promise<string | undefined> {
    const data: PromptDialogData = {
      title: this.translate.instant(options.titleKey ?? labelKey),
      label: this.translate.instant(labelKey),
      value: options.value,
      multiline: options.multiline,
      required: options.required,
    };
    const ref = this.dialog.open(PromptDialogComponent, {
      width: '450px',
      panelClass: 'responsive-dialog',
      data,
    });
    return await firstValueFrom(ref.afterClosed());
  }
}
