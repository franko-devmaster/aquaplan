import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SyncService } from './services/sync.service';
import { LocaleService } from './services/locale.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: [],
  // F-043 — OnPush like every other component in the app.
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit {
  private readonly syncService = inject(SyncService);
  // F-044 — inject LocaleService so it is instantiated at boot and becomes the
  // single owner of i18n init. The duplicate setDefaultLang/use that lived here
  // was removed.
  private readonly localeService = inject(LocaleService);

  ngOnInit(): void {
    // AQ-376 — subscribe to browser connectivity events, flush any actions
    // that were queued offline during the previous session.
    this.syncService.startListening();
  }
}
