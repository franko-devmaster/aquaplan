import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="app-footer">
      AquaPlan v{{ version }} — {{ 'footer.developedBy' | translate }} {{ author }}
    </footer>
  `,
  styles: [`
    .app-footer {
      padding: 8px 16px;
      text-align: center;
      color: #9e9e9e;
      font-size: 11px;
      border-top: 1px solid rgba(0, 0, 0, 0.06);
      background: #fafafa;
    }
  `],
})
export class FooterComponent {
  protected readonly version = environment.version;
  protected readonly author = environment.author;
}
