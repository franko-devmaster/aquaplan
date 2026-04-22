import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { ResultsApiService } from '../../services/results-api.service';
import { RecentResultDto, ResultConformity } from '../../models/results.model';
import {
  SamplingResultsDialogComponent,
  SamplingResultsDialogData,
} from './sampling-results-dialog.component';

/**
 * AQ-415 — Recent results zone (7-day window) shown on top of the /results page.
 * Each card is clickable and opens the shared results dialog.
 */
@Component({
  selector: 'app-recent-results-zone',
  standalone: true,
  imports: [
    DatePipe,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="recent-zone">
      <header class="zone-header">
        <h3>{{ 'results.recentZone.title' | translate }}</h3>
      </header>

      @if (loading()) {
        <div class="loading">
          <mat-spinner diameter="28"></mat-spinner>
        </div>
      } @else if (items().length === 0) {
        <div class="empty">
          <mat-icon>hourglass_empty</mat-icon>
          <p>{{ 'results.recentZone.empty' | translate }}</p>
        </div>
      } @else {
        <div class="cards">
          @for (item of items(); track item.orderId) {
            <button
              type="button"
              class="card"
              [class]="'conformity-' + item.conformity.toLowerCase()"
              (click)="openDialog(item)">
              <div class="card-head">
                <span class="order-number">{{ item.orderNumber }}</span>
                <span class="pill"
                      [class]="'pill-' + item.conformity.toLowerCase()"
                      [matTooltip]="conformityTooltip(item.conformity) | translate">
                  {{ conformityLabel(item.conformity) | translate }}
                </span>
              </div>
              <div class="location">{{ item.locationCode }} — {{ item.locationName }}</div>
              @if (item.programName) {
                <div class="program">{{ item.programName }}</div>
              }
              <div class="received-at">{{ item.receivedAt | date: 'dd.MM.yyyy HH:mm' }}</div>
            </button>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    .recent-zone {
      margin-bottom: 24px;
      padding: 16px;
      background: #fff;
      border-radius: 8px;
      border: 1px solid #e0e0e0;
    }
    .zone-header h3 {
      margin: 0 0 12px 0;
      font-size: 14px;
      text-transform: uppercase;
      letter-spacing: 0.4px;
      color: #424242;
    }
    .loading {
      display: flex;
      justify-content: center;
      padding: 24px;
    }
    .empty {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 20px;
      color: #9e9e9e;
      font-size: 14px;
    }
    .empty p { margin: 0; }
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 12px;
    }
    .card {
      text-align: left;
      padding: 12px;
      border-radius: 8px;
      border: 1px solid #e0e0e0;
      background: #fafafa;
      cursor: pointer;
      font-family: inherit;
      font-size: 13px;
      transition: box-shadow 0.15s ease-out, transform 0.15s ease-out;
    }
    .card:hover {
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.12);
      transform: translateY(-1px);
    }
    .card-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 6px;
    }
    .order-number { font-weight: 600; color: #212121; }
    .location { color: #424242; font-size: 13px; }
    .program { color: #616161; font-size: 12px; margin-top: 2px; }
    .received-at { color: #9e9e9e; font-size: 11px; margin-top: 6px; }

    .pill {
      padding: 2px 8px;
      border-radius: 10px;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.2px;
    }
    .pill-green { background: #E8F5E9; color: #2E7D32; }
    .pill-yellow { background: #FFF3E0; color: #E65100; }
    .pill-red { background: #FFEBEE; color: #B71C1C; }
    .pill-pending { background: #ECEFF1; color: #546E7A; }

    .card.conformity-red { border-left: 3px solid #f44336; }
    .card.conformity-yellow { border-left: 3px solid #ff9800; }
    .card.conformity-green { border-left: 3px solid #4caf50; }
    .card.conformity-pending { border-left: 3px solid #9e9e9e; }
  `],
})
export class RecentResultsZoneComponent implements OnInit {
  private readonly api = inject(ResultsApiService);
  private readonly dialog = inject(MatDialog);

  readonly items = signal<RecentResultDto[]>([]);
  readonly loading = signal(false);

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getRecent(7, 50));
      this.items.set(data ?? []);
    } catch {
      this.items.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  conformityLabel(c: ResultConformity): string {
    switch (c) {
      case 'Green':
        return 'results.conformity.conform';
      case 'Yellow':
        return 'results.conformity.minorAnomaly';
      case 'Red':
        return 'results.conformity.criticalAnomaly';
      default:
        return 'results.conformity.pending';
    }
  }

  conformityTooltip(c: ResultConformity): string {
    return this.conformityLabel(c);
  }

  openDialog(item: RecentResultDto): void {
    this.dialog.open<SamplingResultsDialogComponent, SamplingResultsDialogData>(
      SamplingResultsDialogComponent,
      {
        data: {
          orderId: item.orderId,
          orderNumber: item.orderNumber,
          locationName: `${item.locationCode} — ${item.locationName}`,
        },
        width: '900px',
        maxWidth: '95vw',
        autoFocus: false,
      },
    );
  }
}
