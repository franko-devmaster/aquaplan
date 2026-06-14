import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';
import { SamplingResultsTableComponent } from '../../components/sampling-results-table/sampling-results-table.component';

export interface SamplingResultsDialogData {
  orderId: string;
  orderNumber?: string;
  locationName?: string;
}

/**
 * AQ-415 — Dialog wrapper around <app-sampling-results-table> (AQ-400).
 * Opened when the user clicks a recent-result card or a matrix cell.
 */
@Component({
  selector: 'app-sampling-results-dialog',
  standalone: true,
  imports: [
    MatDialogModule,
    MatIconModule,
    MatButtonModule,
    TranslateModule,
    SamplingResultsTableComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title class="dialog-title">
      <span class="title-text">
        {{ 'results.dialog.title' | translate }}
        @if (data.orderNumber) {
          — {{ data.orderNumber }}
        }
      </span>
      <button mat-icon-button mat-dialog-close [attr.aria-label]="'a11y.close' | translate">
        <mat-icon aria-hidden="true">close</mat-icon>
      </button>
    </h2>
    @if (data.locationName) {
      <p class="location-sub">{{ data.locationName }}</p>
    }
    <mat-dialog-content>
      <app-sampling-results-table [orderId]="data.orderId"></app-sampling-results-table>
    </mat-dialog-content>
  `,
  styles: [`
    .dialog-title {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin: 0;
    }
    .title-text {
      font-size: 18px;
    }
    .location-sub {
      margin: 0 24px 8px 24px;
      color: #616161;
      font-size: 13px;
    }
    mat-dialog-content {
      min-width: 320px;
      max-width: 960px;
    }
  `],
})
export class SamplingResultsDialogComponent {
  // F-034 — the injected MatDialogRef and close() method were dead: the template
  // closes via the `mat-dialog-close` directive directly.
  protected readonly data = inject<SamplingResultsDialogData>(MAT_DIALOG_DATA);
}
