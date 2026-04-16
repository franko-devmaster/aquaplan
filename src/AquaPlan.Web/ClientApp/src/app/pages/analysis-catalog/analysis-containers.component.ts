import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ContainerDatastore } from '../../datastore/container.datastore';
import { ContainerAddDto, ContainerUpdateDto } from '../../models/container.model';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-analysis-containers',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatProgressSpinnerModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatTooltipModule, MatSlideToggleModule,
    FormsModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'analysisCatalog.containers.title' | translate }}</h2>
      @if (isAdmin()) {
        <button mat-raised-button color="primary" (click)="openCreateForm()">
          <mat-icon>add</mat-icon>
          {{ 'analysisCatalog.containers.create' | translate }}
        </button>
      }
    </div>

    <div class="filters">
      <mat-form-field appearance="outline">
        <mat-label>{{ 'common.search' | translate }}</mat-label>
        <input matInput [(ngModel)]="searchText" (ngModelChange)="applyFilter()">
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <table mat-table [dataSource]="store.containers()" class="full-width">
        <ng-container matColumnDef="code">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.containers.code' | translate }}</th>
          <td mat-cell *matCellDef="let c">{{ c.code }}</td>
        </ng-container>

        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.containers.name' | translate }}</th>
          <td mat-cell *matCellDef="let c">{{ c.name }}</td>
        </ng-container>

        <ng-container matColumnDef="material">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.containers.material' | translate }}</th>
          <td mat-cell *matCellDef="let c">{{ c.material }}</td>
        </ng-container>

        <ng-container matColumnDef="volume">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.containers.volume' | translate }}</th>
          <td mat-cell *matCellDef="let c">{{ c.volumeMl }} ml</td>
        </ng-container>

        <ng-container matColumnDef="color">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.containers.color' | translate }}</th>
          <td mat-cell *matCellDef="let c">{{ c.color }}</td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | translate }}</th>
          <td mat-cell *matCellDef="let c">
            <mat-chip class="status-chip" [class.inactive]="!c.isActive">
              {{ (c.isActive ? 'common.active' : 'common.inactive') | translate }}
            </mat-chip>
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let c" (click)="$event.stopPropagation()">
            @if (isAdmin()) {
              <mat-slide-toggle
                [checked]="c.isActive"
                (change)="toggle(c.id)"
                [matTooltip]="'common.toggleStatus' | translate">
              </mat-slide-toggle>
            }
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns;"
            class="clickable-row" (click)="openViewForm(row)"></tr>
      </table>

      @if (store.containers().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }

    @if (showForm()) {
      <div class="form-overlay" (click)="closeForm()">
        <div class="form-panel" (click)="$event.stopPropagation()">
          <h3>{{ (formReadonly() ? 'analysisCatalog.containers.view' : (editingId() ? 'analysisCatalog.containers.edit' : 'analysisCatalog.containers.create')) | translate }}</h3>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.containers.code' | translate }}</mat-label>
            <input matInput [(ngModel)]="formCode" [readonly]="formReadonly()" required>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.containers.name' | translate }}</mat-label>
            <input matInput [(ngModel)]="formName" [readonly]="formReadonly()" required>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.containers.material' | translate }}</mat-label>
            <input matInput [(ngModel)]="formMaterial" [readonly]="formReadonly()" required>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.containers.volume' | translate }}</mat-label>
            <input matInput type="number" [(ngModel)]="formVolumeMl" [readonly]="formReadonly()" required>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.containers.color' | translate }}</mat-label>
            <input matInput [(ngModel)]="formColor" [readonly]="formReadonly()" required>
          </mat-form-field>
          <div class="form-actions">
            <button mat-button (click)="closeForm()">{{ (formReadonly() ? 'common.close' : 'common.cancel') | translate }}</button>
            @if (!formReadonly()) {
              <button mat-raised-button color="primary" (click)="saveContainer()" [disabled]="!canSave()">{{ 'common.save' | translate }}</button>
            }
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .full-width { width: 100%; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .inactive { opacity: 0.6; }
    .filters { display: flex; gap: 16px; margin-bottom: 16px; }
    .filters mat-form-field { min-width: 200px; }
    .form-overlay {
      position: fixed; top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(0,0,0,0.4); display: flex; justify-content: center; align-items: center; z-index: 1000;
    }
    .form-panel {
      background: white; padding: 24px; border-radius: 8px; min-width: 400px; max-width: 500px;
    }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 16px; }
    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background-color: rgba(0, 0, 0, 0.04); }
  `],
})
export class AnalysisContainersComponent implements OnInit {
  readonly store = inject(ContainerDatastore);
  private readonly authService = inject(AuthService);

  readonly displayedColumns = ['code', 'name', 'material', 'volume', 'color', 'status', 'actions'];

  searchText = '';

  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formReadonly = signal(false);

  formCode = '';
  formName = '';
  formMaterial = '';
  formVolumeMl = 0;
  formColor = '';

  ngOnInit(): void {
    this.store.loadAll();
  }

  isAdmin(): boolean {
    const user = this.authService.currentUser();
    return user?.roles.includes('Administrator') ?? false;
  }

  canSave(): boolean {
    return !!this.formCode && !!this.formName && !!this.formMaterial && this.formVolumeMl > 0 && !!this.formColor;
  }

  applyFilter(): void {
    this.store.loadAll({ search: this.searchText || undefined });
  }

  openCreateForm(): void {
    this.editingId.set(null);
    this.formCode = '';
    this.formName = '';
    this.formMaterial = '';
    this.formVolumeMl = 0;
    this.formColor = '';
    this.formReadonly.set(false);
    this.showForm.set(true);
  }

  openViewForm(container: { id: string; code: string; name: string; material: string; volumeMl: number; color: string }): void {
    this.editingId.set(container.id);
    this.formCode = container.code;
    this.formName = container.name;
    this.formMaterial = container.material;
    this.formVolumeMl = container.volumeMl;
    this.formColor = container.color;
    this.formReadonly.set(!this.isAdmin());
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  async saveContainer(): Promise<void> {
    if (this.editingId()) {
      const dto: ContainerUpdateDto = {
        code: this.formCode,
        name: this.formName,
        material: this.formMaterial,
        volumeMl: this.formVolumeMl,
        color: this.formColor,
        isActive: true,
      };
      await this.store.update(this.editingId()!, dto);
    } else {
      const dto: ContainerAddDto = {
        code: this.formCode,
        name: this.formName,
        material: this.formMaterial,
        volumeMl: this.formVolumeMl,
        color: this.formColor,
      };
      await this.store.create(dto);
    }
    this.closeForm();
  }

  async toggle(id: string): Promise<void> {
    await this.store.toggleStatus(id);
  }
}
