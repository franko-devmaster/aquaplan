import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';

export interface RejectDialogResult {
  comment: string;
}

@Component({
  selector: 'app-reject-dialog',
  standalone: true,
  imports: [
    MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatInputModule, FormsModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'changeRequests.confirmReject' | translate }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>{{ 'changeRequests.reviewComment' | translate }}</mat-label>
        <textarea matInput [(ngModel)]="comment" rows="3" required></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="warn" [disabled]="!comment.trim()" (click)="confirm()">
        {{ 'changeRequests.reject' | translate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; }
  `],
})
export class RejectDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<RejectDialogComponent>);

  comment = '';

  confirm(): void {
    if (this.comment.trim()) {
      this.dialogRef.close({ comment: this.comment.trim() } as RejectDialogResult);
    }
  }
}
