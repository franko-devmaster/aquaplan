import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type StatusChipVariant = 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'draft';

@Component({
  selector: 'app-status-chip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="status-chip {{ variant() }}">{{ label() }}</span>`,
  styles: [`
    .status-chip {
      display: inline-block;
      border-radius: 4px;
      padding: 2px 8px;
      font-size: 11px;
      font-weight: 500;
      letter-spacing: 0.2px;
      background: #F5F5F7;
      color: #424242;
      border: 1px solid rgba(0, 0, 0, 0.08);
      line-height: 1.4;
    }
    .status-chip.info { background: #E3F2FD; color: #1565C0; }
    .status-chip.success { background: #E8F5E9; color: #2E7D32; }
    .status-chip.warning { background: #FFF3E0; color: #E65100; }
    .status-chip.danger { background: #FFEBEE; color: #C62828; }
    .status-chip.draft { background: #F5F5F7; color: #616161; }
  `],
})
export class StatusChipComponent {
  readonly label = input.required<string>();
  readonly variant = input<StatusChipVariant>('neutral');
}
