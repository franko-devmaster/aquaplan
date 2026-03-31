import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: [],
})
export class AppComponent {
  private readonly translate = inject(TranslateService);

  constructor() {
    this.translate.setDefaultLang('fr');
    this.translate.use('fr');
  }
}
