import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { RecentResultsZoneComponent } from './recent-results-zone.component';
import { ResultsMatrixComponent } from './results-matrix.component';

/**
 * AQ-415 — /results page. Two stacked sections:
 *   - Recent results zone (7-day window, card grid)
 *   - LDP × dates matrix with filters
 * Both open the same MatDialog hosting <app-sampling-results-table> (AQ-400).
 */
@Component({
  selector: 'app-results',
  standalone: true,
  imports: [TranslateModule, RecentResultsZoneComponent, ResultsMatrixComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="results-page">
      <header class="page-header">
        <h2>{{ 'results.title' | translate }}</h2>
      </header>
      <app-recent-results-zone></app-recent-results-zone>
      <app-results-matrix></app-results-matrix>
    </div>
  `,
  styles: [`
    .results-page {
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0;
    }
    .page-header h2 {
      margin: 0 0 16px 0;
    }
  `],
})
export class ResultsComponent {}
