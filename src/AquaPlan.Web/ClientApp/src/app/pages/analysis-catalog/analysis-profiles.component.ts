import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { AnalysisProfileDatastore } from '../../datastore/analysis-profile.datastore';
import { AnalysisProfileAddDto, AnalysisProfileUpdateDto, AnalysisCategory } from '../../models/analysis-profile.model';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-analysis-profiles',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatProgressSpinnerModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatTooltipModule, FormsModule,
    TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'analysisCatalog.profiles.title' | translate }}</h2>
      @if (isAdmin()) {
        <button mat-raised-button color="primary" (click)="openCreateForm()">
          <mat-icon>add</mat-icon>
          {{ 'analysisCatalog.profiles.create' | translate }}
        </button>
      }
    </div>

    <div class="filters">
      <mat-form-field appearance="outline">
        <mat-label>{{ 'common.search' | translate }}</mat-label>
        <input matInput [(ngModel)]="searchText" (ngModelChange)="applyFilter()">
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>{{ 'analysisCatalog.profiles.category' | translate }}</mat-label>
        <mat-select [(ngModel)]="selectedCategory" (ngModelChange)="applyFilter()">
          <mat-option>{{ 'common.all' | translate }}</mat-option>
          @for (cat of categories; track cat) {
            <mat-option [value]="cat">{{ 'analysisCatalog.categories.' + cat | translate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    @if (store.loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <table mat-table [dataSource]="store.profiles()" class="full-width">
        <ng-container matColumnDef="code">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.profiles.code' | translate }}</th>
          <td mat-cell *matCellDef="let p">{{ p.code }}</td>
        </ng-container>

        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.profiles.name' | translate }}</th>
          <td mat-cell *matCellDef="let p">{{ p.name }}</td>
        </ng-container>

        <ng-container matColumnDef="category">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.profiles.category' | translate }}</th>
          <td mat-cell *matCellDef="let p">
            <mat-chip>{{ 'analysisCatalog.categories.' + p.category | translate }}</mat-chip>
          </td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | translate }}</th>
          <td mat-cell *matCellDef="let p">
            <mat-chip class="status-chip" [class.inactive]="!p.isActive">
              {{ (p.isActive ? 'common.active' : 'common.inactive') | translate }}
            </mat-chip>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns;"
            class="clickable-row" (click)="openEditForm(row)"></tr>
      </table>

      @if (store.profiles().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }

    @if (showForm()) {
      <div class="form-overlay" (click)="closeForm()">
        <div class="form-panel" (click)="$event.stopPropagation()">
          <h3>{{ (editingId() ? 'analysisCatalog.profiles.edit' : 'analysisCatalog.profiles.create') | translate }}</h3>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.profiles.code' | translate }}</mat-label>
            <input matInput [(ngModel)]="formCode">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.profiles.name' | translate }}</mat-label>
            <input matInput [(ngModel)]="formName">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.profiles.description' | translate }}</mat-label>
            <textarea matInput [(ngModel)]="formDescription" rows="3"></textarea>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.profiles.category' | translate }}</mat-label>
            <mat-select [(ngModel)]="formCategory">
              @for (cat of categories; track cat) {
                <mat-option [value]="cat">{{ 'analysisCatalog.categories.' + cat | translate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <div class="form-actions">
            <button mat-button (click)="closeForm()">{{ 'common.cancel' | translate }}</button>
            <button mat-raised-button color="primary" (click)="saveProfile()">{{ 'common.save' | translate }}</button>
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
  `],
})
export class AnalysisProfilesComponent implements OnInit {
  readonly store = inject(AnalysisProfileDatastore);
  private readonly authService = inject(AuthService);

  readonly displayedColumns = ['code', 'name', 'category', 'status'];
  readonly categories: AnalysisCategory[] = ['Bacteriology', 'Chemistry', 'Physical', 'Other'];

  searchText = '';
  selectedCategory: AnalysisCategory | undefined;

  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  formCode = '';
  formName = '';
  formDescription = '';
  formCategory: AnalysisCategory = 'Other';

  ngOnInit(): void {
    this.store.loadAll();
  }

  isAdmin(): boolean {
    const user = this.authService.currentUser();
    return user?.roles.includes('Administrator') ?? false;
  }

  applyFilter(): void {
    this.store.loadAll({
      search: this.searchText || undefined,
      category: this.selectedCategory,
    });
  }

  openCreateForm(): void {
    this.editingId.set(null);
    this.formCode = '';
    this.formName = '';
    this.formDescription = '';
    this.formCategory = 'Other';
    this.showForm.set(true);
  }

  openEditForm(profile: { id: string; code: string; name: string; description?: string; category: AnalysisCategory }): void {
    this.editingId.set(profile.id);
    this.formCode = profile.code;
    this.formName = profile.name;
    this.formDescription = profile.description ?? '';
    this.formCategory = profile.category;
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  async saveProfile(): Promise<void> {
    if (this.editingId()) {
      const dto: AnalysisProfileUpdateDto = {
        code: this.formCode,
        name: this.formName,
        description: this.formDescription || null,
        category: this.formCategory,
        isActive: true,
      };
      await this.store.update(this.editingId()!, dto);
    } else {
      const dto: AnalysisProfileAddDto = {
        code: this.formCode,
        name: this.formName,
        description: this.formDescription || null,
        category: this.formCategory,
      };
      await this.store.create(dto);
    }
    this.closeForm();
  }

}
