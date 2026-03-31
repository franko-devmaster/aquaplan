import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DecimalPipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { SamplingLocationDatastore } from '../../datastore/sampling-location.datastore';

@Component({
  selector: 'app-sampling-location-list',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatProgressSpinnerModule, DecimalPipe, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'samplingLocations.title' | translate }}</h2>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <table mat-table [dataSource]="store.locations()" class="full-width">
        <ng-container matColumnDef="locationCode">
          <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.locationCode' | translate }}</th>
          <td mat-cell *matCellDef="let loc">{{ loc.locationCode }}</td>
        </ng-container>

        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.name' | translate }}</th>
          <td mat-cell *matCellDef="let loc">{{ loc.name }}</td>
        </ng-container>

        <ng-container matColumnDef="distributor">
          <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.distributor' | translate }}</th>
          <td mat-cell *matCellDef="let loc">{{ loc.distributorName }}</td>
        </ng-container>

        <ng-container matColumnDef="coordinates">
          <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.latitude' | translate }} / {{ 'samplingLocations.longitude' | translate }}</th>
          <td mat-cell *matCellDef="let loc">
            @if (loc.latitude && loc.longitude) {
              {{ loc.latitude | number:'1.4-4' }}, {{ loc.longitude | number:'1.4-4' }}
            } @else {
              -
            }
          </td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'samplingLocations.status' | translate }}</th>
          <td mat-cell *matCellDef="let loc">
            <mat-chip [class.inactive]="!loc.isActive">
              {{ (loc.isActive ? 'common.active' : 'common.inactive') | translate }}
            </mat-chip>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
      </table>

      @if (store.locations().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .inactive { opacity: 0.6; }
  `],
})
export class SamplingLocationListComponent implements OnInit {
  readonly store = inject(SamplingLocationDatastore);

  readonly displayedColumns = ['locationCode', 'name', 'distributor', 'coordinates', 'status'];

  ngOnInit(): void {
    this.store.loadAll();
  }
}
