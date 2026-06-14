import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslateModule } from '@ngx-translate/core';

export interface PromptDialogData {
  /** Already-translated dialog title. */
  title: string;
  /** Already-translated field label / prompt message. */
  label: string;
  /** Initial value of the input. */
  value?: string;
  /** When true a multi-line textarea is shown (e.g. rejection reason). */
  multiline?: boolean;
  /** When true an empty value cannot be confirmed. */
  required?: boolean;
}

/**
 * F-011 — Material replacement for the native `prompt()` (sampler comment, plan
 * rejection reason). Returns the entered string on confirm, or `undefined` when
 * cancelled (distinguishable from an empty string).
 */
@Component({
  selector: 'app-prompt-dialog',
  standalone: true,
  imports: [
    FormsModule, MatDialogModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ data.label }}</mat-label>
        @if (data.multiline) {
          <textarea matInput rows="4" [(ngModel)]="text" cdkFocusInitial></textarea>
        } @else {
          <input matInput [(ngModel)]="text" cdkFocusInitial />
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary"
              [disabled]="data.required && !text().trim()"
              (click)="confirm()">{{ 'common.confirm' | translate }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; }
  `],
})
export class PromptDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<PromptDialogComponent, string | undefined>);
  readonly data: PromptDialogData = inject(MAT_DIALOG_DATA);
  readonly text = signal<string>(this.data.value ?? '');

  cancel(): void {
    this.dialogRef.close(undefined);
  }

  confirm(): void {
    this.dialogRef.close(this.text());
  }
}
