import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { inject } from '@angular/core';

/**
 * AQ-414 — Shape of the indicator data consumed by the component.
 * All fields are optional / booleans so the caller can feed whatever it has.
 */
export interface OrderIndicatorsInput {
  hasMandatorNote: boolean;
  hasPreleveurNote: boolean;
  hasReplacedLocation: boolean;
  mandatorNote?: string | null;
  preleveurNote?: string | null;
  locationReplacementReason?: string | null;
}

/**
 * AQ-414 — Renders up to three small Material icons for a sampling-round order:
 *   • sticky_note_2  — mandator note (Order.Notes)
 *   • comment        — préleveur remark (Sampling.Notes)
 *   • swap_horiz     — location was replaced (OriginalSamplingLocationId set)
 *
 * Each icon carries a Material tooltip showing the associated text when available
 * or a generic i18n label when it isn't. The component renders nothing when all
 * three flags are false, keeping the round table compact.
 */
@Component({
  selector: 'app-order-indicators',
  standalone: true,
  imports: [MatIconModule, MatTooltipModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (hasAny()) {
      <span class="indicators">
        @if (data().hasMandatorNote) {
          <mat-icon class="indicator mandator"
                    [attr.aria-label]="'a11y.mandatorNote' | translate"
                    [matTooltip]="mandatorTooltip()"
                    matTooltipShowDelay="200">
            sticky_note_2
          </mat-icon>
        }
        @if (data().hasPreleveurNote) {
          <mat-icon class="indicator preleveur"
                    [attr.aria-label]="'a11y.preleveurNote' | translate"
                    [matTooltip]="preleveurTooltip()"
                    matTooltipShowDelay="200">
            comment
          </mat-icon>
        }
        @if (data().hasReplacedLocation) {
          <mat-icon class="indicator replaced"
                    [attr.aria-label]="'a11y.locationReplaced' | translate"
                    [matTooltip]="replacedTooltip()"
                    matTooltipShowDelay="200">
            swap_horiz
          </mat-icon>
        }
      </span>
    }
  `,
  styles: [`
    .indicators {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .indicator {
      font-size: 18px;
      width: 18px;
      height: 18px;
      cursor: default;
    }
    .indicator.mandator { color: #1976D2; }
    .indicator.preleveur { color: #7B1FA2; }
    .indicator.replaced { color: #F57C00; }
  `],
})
export class OrderIndicatorsComponent {
  private readonly translate = inject(TranslateService);

  readonly data = input.required<OrderIndicatorsInput>();

  readonly hasAny = computed(() => {
    const d = this.data();
    return d.hasMandatorNote || d.hasPreleveurNote || d.hasReplacedLocation;
  });

  readonly mandatorTooltip = computed(() => {
    const d = this.data();
    return d.mandatorNote?.trim() || this.translate.instant('orderIndicators.mandatorNote');
  });

  readonly preleveurTooltip = computed(() => {
    const d = this.data();
    return d.preleveurNote?.trim() || this.translate.instant('orderIndicators.preleveurNote');
  });

  readonly replacedTooltip = computed(() => {
    const d = this.data();
    return d.locationReplacementReason?.trim() || this.translate.instant('orderIndicators.locationReplaced');
  });
}
