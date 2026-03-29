import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ 'app.title' | translate }}</h1>
    <p>{{ 'app.subtitle' | translate }}</p>
  `,
})
export class HomeComponent {}
