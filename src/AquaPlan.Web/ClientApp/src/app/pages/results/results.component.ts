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
        <h1 class="page-title">{{ 'results.title' | translate }}</h1>
      </header>
      <app-recent-results-zone></app-recent-results-zone>
      <app-results-matrix></app-results-matrix>
    </div>
  `,
  styles: [`
    :host { display: block; background: var(--color-bg-page); }
    .results-page {
      padding: var(--space-6);
      max-width: 1440px;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: var(--space-5);
    }
    .page-header { margin-bottom: var(--space-2); }
    .page-title {
      margin: 0;
      font-family: var(--font-family-base);
      font-size: var(--font-size-26);
      font-weight: var(--font-weight-semibold);
      color: var(--color-fg-default);
      letter-spacing: var(--letter-spacing-tight);
    }
    @media (max-width: 768px) {
      .results-page { padding: var(--space-3); gap: var(--space-4); }
      .page-title { font-size: var(--font-size-22); }
    }
  `],
})
export class ResultsComponent {}
