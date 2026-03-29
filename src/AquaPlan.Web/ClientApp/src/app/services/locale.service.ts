import { Injectable, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Injectable({ providedIn: 'root' })
export class LocaleService {
  readonly currentLang = signal('fr');
  private readonly supportedLangs = ['fr', 'de', 'en'];

  constructor(private readonly translate: TranslateService) {
    this.translate.addLangs(this.supportedLangs);
    this.translate.setDefaultLang('fr');
    this.translate.use('fr');
  }

  switchLanguage(lang: string): void {
    if (this.supportedLangs.includes(lang)) {
      this.translate.use(lang);
      this.currentLang.set(lang);
    }
  }

  getSupportedLanguages(): string[] {
    return [...this.supportedLangs];
  }
}
