import { Injectable, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

const STORAGE_KEY = 'aq_lang';

/**
 * F-044 — single source of truth for i18n initialisation. app.config provides
 * the TranslateModule + default language; this service (instantiated at boot via
 * AppComponent injection) owns addLangs / setDefaultLang / use and the runtime
 * language switch. The previous duplicate init in AppComponent's constructor was
 * removed. F-046 — uses inject() instead of constructor injection.
 * The chosen language is now persisted in sessionStorage and restored on reload.
 */
@Injectable({ providedIn: 'root' })
export class LocaleService {
  private readonly translate = inject(TranslateService);

  private readonly supportedLangs = ['fr', 'de', 'en'];
  readonly currentLang = signal('fr');

  constructor() {
    this.translate.addLangs(this.supportedLangs);
    this.translate.setDefaultLang('fr');
    const initial = this.readStoredLang() ?? 'fr';
    this.translate.use(initial);
    this.currentLang.set(initial);
  }

  switchLanguage(lang: string): void {
    if (this.supportedLangs.includes(lang)) {
      this.translate.use(lang);
      this.currentLang.set(lang);
      this.persistLang(lang);
    }
  }

  getSupportedLanguages(): string[] {
    return [...this.supportedLangs];
  }

  private readStoredLang(): string | null {
    try {
      const stored = sessionStorage.getItem(STORAGE_KEY);
      return stored && this.supportedLangs.includes(stored) ? stored : null;
    } catch {
      return null;
    }
  }

  private persistLang(lang: string): void {
    try {
      sessionStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // private mode / quota — non-fatal, language just won't survive reload.
    }
  }
}
