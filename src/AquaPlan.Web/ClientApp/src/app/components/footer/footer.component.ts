import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [TranslateModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="app-footer">
      @if (version) {
        <span [matTooltip]="commitTooltip" matTooltipPosition="above">
          AquaPlan v{{ version }} — {{ 'footer.developedBy' | translate }} {{ author }}
        </span>
      } @else {
        AquaPlan (dev) — {{ 'footer.developedBy' | translate }} {{ author }}
      }
    </footer>
  `,
  styles: [`
    /* AQ-FOOTER-STICKY — footer always visible at bottom of viewport.
       Fixed height (32px) matches the space reserved by the layout container. */
    .app-footer {
      position: fixed;
      left: 0;
      right: 0;
      bottom: 0;
      height: 32px;
      padding: 0 16px;
      display: flex;
      align-items: center;
      justify-content: center;
      text-align: center;
      color: #9e9e9e;
      font-size: 11px;
      border-top: 1px solid rgba(0, 0, 0, 0.06);
      background: #fafafa;
      z-index: 50;
    }
  `],
})
export class FooterComponent {
  protected readonly version = environment.version;
  protected readonly author = environment.author;
  protected readonly commitTooltip = environment.commit
    ? `commit ${environment.commit}`
    : '';
}
