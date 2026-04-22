import { ChangeDetectionStrategy, Component, input } from '@angular/core';

// AQ-423 — aligned with design-system tokens (--chip-*-bg / --chip-*-fg).
// Existing variants (info/success/warning/danger/neutral/draft) are kept for
// callers already using them, mapped to the token palette. Additional metier
// variants (assigned, planned, in-progress, sampled, in-analysis, closed,
// cancelled, conform, non-conform, pending) are exposed for new callers.
export type StatusChipVariant =
  | 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'draft'
  | 'to-plan' | 'planned' | 'in-progress' | 'sampled'
  | 'in-analysis' | 'results' | 'closed' | 'cancelled'
  | 'assigned' | 'conform' | 'non-conform' | 'pending';

@Component({
  selector: 'app-status-chip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="status-chip {{ variant() }}">{{ label() }}</span>`,
  styles: [`
    .status-chip {
      display: inline-block;
      border-radius: var(--radius-chip);
      padding: 2px var(--space-2);
      font-family: var(--font-family-base);
      font-size: var(--font-size-12);
      font-weight: var(--font-weight-medium);
      letter-spacing: 0.2px;
      line-height: 1.4;
      background: var(--chip-to-plan-bg);
      color: var(--chip-to-plan-fg);
      border: 1px solid transparent;
    }

    /* Legacy semantic variants — mapped to new tokens */
    .status-chip.info       { background: var(--color-info-50);    color: var(--color-info-700); }
    .status-chip.success    { background: var(--color-success-50); color: var(--color-success-700); }
    .status-chip.warning    { background: var(--color-warning-50); color: var(--color-warning-700); }
    .status-chip.danger     { background: var(--color-error-50);   color: var(--color-error-700); }
    .status-chip.neutral,
    .status-chip.draft      { background: var(--chip-draft-bg);    color: var(--chip-draft-fg); }

    /* Orders workflow palette */
    .status-chip.to-plan      { background: var(--chip-to-plan-bg);     color: var(--chip-to-plan-fg); }
    .status-chip.planned      { background: var(--chip-planned-bg);     color: var(--chip-planned-fg); }
    .status-chip.in-progress  { background: var(--chip-in-progress-bg); color: var(--chip-in-progress-fg); }
    .status-chip.sampled      { background: var(--chip-sampled-bg);     color: var(--chip-sampled-fg); }
    .status-chip.in-analysis  { background: var(--chip-in-analysis-bg); color: var(--chip-in-analysis-fg); }
    .status-chip.results      { background: var(--chip-results-bg);     color: var(--chip-results-fg); }
    .status-chip.closed       { background: var(--chip-closed-bg);      color: var(--chip-closed-fg); }
    .status-chip.cancelled    { background: var(--chip-cancelled-bg);   color: var(--chip-cancelled-fg); }

    /* Rounds */
    .status-chip.assigned     { background: var(--chip-assigned-bg);    color: var(--chip-assigned-fg); }

    /* Results */
    .status-chip.conform      { background: var(--chip-conform-bg);     color: var(--chip-conform-fg); }
    .status-chip.non-conform  { background: var(--chip-non-conform-bg); color: var(--chip-non-conform-fg); }
    .status-chip.pending      { background: var(--chip-pending-bg);     color: var(--chip-pending-fg); }
  `],
})
export class StatusChipComponent {
  readonly label = input.required<string>();
  readonly variant = input<StatusChipVariant>('neutral');
}
