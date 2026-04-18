import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { SyncService } from './services/sync.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: [],
})
export class AppComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly syncService = inject(SyncService);

  constructor() {
    this.translate.setDefaultLang('fr');
    this.translate.use('fr');
  }

  ngOnInit(): void {
    // AQ-376 — subscribe to browser connectivity events, flush any actions
    // that were queued offline during the previous session.
    this.syncService.startListening();
  }
}
