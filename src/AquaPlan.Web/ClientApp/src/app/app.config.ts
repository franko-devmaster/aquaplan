import { ApplicationConfig, provideZoneChangeDetection, importProvidersFrom, isDevMode } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideServiceWorker } from '@angular/service-worker';
import { TranslateModule } from '@ngx-translate/core';
import { TranslateHttpLoader, provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { routes } from './app.routes';
import { authInterceptor } from './handlers/auth.interceptor';
import { environment } from '../environments/environment';

/**
 * AQ-425 — Safari iOS (et parfois macOS) a des bugs sévères avec le Service Worker
 * Angular (ngsw-worker.js) : cache stale, eviction agressive, état imprévisible qui
 * laissent l'app en écran blanc ou coincée sur un vieux bundle. DuckDuckGo iOS
 * utilise aussi WebKit mais sans ces restrictions Safari-spécifiques. On désactive
 * donc le SW uniquement sur Safari — les autres navigateurs (Chrome/Edge/Firefox
 * desktop + Android, DuckDuckGo iOS) gardent le mode offline complet.
 *
 * Détection : userAgent contient "Safari" mais PAS les navigateurs iOS tiers
 * (Chrome/CriOS, Firefox/FxiOS, Edge/EdgiOS, DuckDuckGo, Opera/OPiOS) qui héritent
 * du UA Safari. macOS Safari inclus par prudence — desktop users n'ont pas besoin
 * d'offline de toute façon.
 */
function isSafariBrowser(): boolean {
  if (typeof navigator === 'undefined') return false;
  const ua = navigator.userAgent;
  const isSafariUa = /Safari/i.test(ua);
  const isOtherBrowser = /(Chrome|Chromium|CriOS|FxiOS|EdgiOS|EdgA|DuckDuckGo|OPiOS|OPR|Opera|YaBrowser|SamsungBrowser)/i.test(ua);
  return isSafariUa && !isOtherBrowser;
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    provideNativeDateAdapter(),
    provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' }),
    importProvidersFrom(
      TranslateModule.forRoot({
        defaultLanguage: 'fr',
        loader: { provide: TranslateHttpLoader },
      })
    ),
    // AQ-374 — Service Worker for PWA offline support. Enabled in production
    // builds and whenever pwaEnabled is set so QA can test offline locally.
    // AQ-425 — désactivé sur Safari (bugs SW récurrents), les autres navigateurs
    // (Chrome/Edge/Firefox/DuckDuckGo inclus) gardent l'offline complet.
    provideServiceWorker('ngsw-worker.js', {
      enabled: (environment.production || environment.pwaEnabled) && !isSafariBrowser(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
