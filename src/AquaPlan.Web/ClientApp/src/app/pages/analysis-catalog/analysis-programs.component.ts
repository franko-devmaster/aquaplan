import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatListModule } from '@angular/material/list';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { AnalysisProgramDatastore } from '../../datastore/analysis-program.datastore';
import { AnalysisProfileDatastore } from '../../datastore/analysis-profile.datastore';
import { AnalysisProgramAddDto, AnalysisProgramUpdateDto } from '../../models/analysis-program.model';
import { AuthService } from '../../services/auth.service';
import { StatusChipComponent } from '../../components/status-chip/status-chip.component';

@Component({
  selector: 'app-analysis-programs',
  standalone: true,
  imports: [
    MatTableModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatProgressSpinnerModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatTooltipModule, MatListModule, FormsModule,
    TranslateModule, StatusChipComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'analysisCatalog.programs.title' | translate }}</h2>
      @if (isAdmin()) {
        <button mat-raised-button color="primary" (click)="openCreateForm()">
          <mat-icon>add</mat-icon>
          {{ 'analysisCatalog.programs.create' | translate }}
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
      <table mat-table [dataSource]="store.programs()" class="full-width">
        <ng-container matColumnDef="code">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.programs.code' | translate }}</th>
          <td mat-cell *matCellDef="let p">{{ p.code }}</td>
        </ng-container>

        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.programs.name' | translate }}</th>
          <td mat-cell *matCellDef="let p">{{ p.name }}</td>
        </ng-container>

        <ng-container matColumnDef="profileCount">
          <th mat-header-cell *matHeaderCellDef>{{ 'analysisCatalog.programs.profileCount' | translate }}</th>
          <td mat-cell *matCellDef="let p">{{ p.profileCount }}</td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | translate }}</th>
          <td mat-cell *matCellDef="let p">
            <app-status-chip [variant]="p.isActive ? 'success' : 'draft'"
                             [label]="((p.isActive ? 'common.active' : 'common.inactive') | translate)"></app-status-chip>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: displayedColumns;"
            class="clickable-row" (click)="openManageProfiles(row.id)"></tr>
      </table>

      @if (store.programs().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }

    @if (showForm()) {
      <div class="form-overlay" (click)="closeForm()">
        <div class="form-panel" (click)="$event.stopPropagation()">
          <h3>{{ (editingId() ? 'analysisCatalog.programs.edit' : 'analysisCatalog.programs.create') | translate }}</h3>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.programs.code' | translate }}</mat-label>
            <input matInput [(ngModel)]="formCode">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.programs.name' | translate }}</mat-label>
            <input matInput [(ngModel)]="formName">
          </mat-form-field>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'analysisCatalog.programs.description' | translate }}</mat-label>
            <textarea matInput [(ngModel)]="formDescription" rows="3"></textarea>
          </mat-form-field>
          <div class="form-actions">
            <button mat-button (click)="closeForm()">{{ 'common.cancel' | translate }}</button>
            <button mat-raised-button color="primary" (click)="saveProgram()">{{ 'common.save' | translate }}</button>
          </div>
        </div>
      </div>
    }

    @if (showProfilePanel()) {
      <div class="form-overlay" (click)="closeProfilePanel()">
        <div class="form-panel profiles-panel" (click)="$event.stopPropagation()">
          <h3>{{ 'analysisCatalog.programs.manageProfiles' | translate }}</h3>

          @if (store.selectedProgram(); as program) {
            <h4>{{ program.name }}</h4>

            <div class="profile-list">
              <h5>{{ 'analysisCatalog.programs.assignedProfiles' | translate }}</h5>
              @if (program.profiles.length === 0) {
                <p class="no-data">{{ 'common.noData' | translate }}</p>
              }
              @for (profile of program.profiles; track profile.id) {
                <div class="profile-item">
                  <span>{{ profile.code }} - {{ profile.name }}</span>
                  @if (isAdmin()) {
                    <button mat-icon-button color="warn" (click)="removeProfile(program.id, profile.id)">
                      <mat-icon>remove_circle</mat-icon>
                    </button>
                  }
                </div>
              }
            </div>

            <div class="required-containers">
              <h5>{{ 'analysisCatalog.programs.requiredContainers' | translate }}</h5>
              @if (program.requiredContainers.length === 0) {
                <p class="no-data">{{ 'analysisCatalog.programs.noContainers' | translate }}</p>
              } @else {
                <mat-chip-set>
                  @for (container of program.requiredContainers; track container.containerId) {
                    <mat-chip>
                      {{ container.code }} — {{ container.name }} ({{ container.volumeMl }} ml)
                      @if (container.profileCount > 1) {
                        <span class="profile-count">× {{ container.profileCount }} profils</span>
                      }
                    </mat-chip>
                  }
                </mat-chip-set>
              }
            </div>

            @if (isAdmin()) {
              <div class="add-profile-section">
                <h5>{{ 'analysisCatalog.programs.addProfile' | translate }}</h5>
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>{{ 'analysisCatalog.profiles.title' | translate }}</mat-label>
                  <mat-select [(ngModel)]="selectedProfileIds" multiple>
                    @for (profile of availableProfiles(); track profile.id) {
                      <mat-option [value]="profile.id">{{ profile.code }} - {{ profile.name }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <button mat-raised-button color="primary" (click)="addProfiles()" [disabled]="selectedProfileIds.length === 0">
                  {{ 'analysisCatalog.programs.addProfile' | translate }}
                </button>
              </div>
            }
          }

          <div class="form-actions">
            <button mat-button (click)="closeProfilePanel()">{{ 'common.close' | translate }}</button>
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
      background: white; padding: 24px; border-radius: 8px; min-width: 400px; max-width: 600px;
    }
    .profiles-panel { max-height: 80vh; overflow-y: auto; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 16px; }
    .profile-item { display: flex; justify-content: space-between; align-items: center; padding: 4px 0; }
    .profile-list { margin-bottom: 16px; }
    .required-containers { border-top: 1px solid #e0e0e0; padding-top: 16px; margin-bottom: 16px; }
    .required-containers mat-chip-set { margin-top: 8px; }
    .profile-count { margin-left: 6px; font-size: 11px; opacity: 0.75; font-style: italic; }
    .add-profile-section { border-top: 1px solid #e0e0e0; padding-top: 16px; }
    h5 { margin: 8px 0; color: #666; }
  `],
})
export class AnalysisProgramsComponent implements OnInit {
  readonly store = inject(AnalysisProgramDatastore);
  private readonly profileStore = inject(AnalysisProfileDatastore);
  private readonly authService = inject(AuthService);

  readonly displayedColumns = ['code', 'name', 'profileCount', 'status'];

  searchText = '';

  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  formCode = '';
  formName = '';
  formDescription = '';

  readonly showProfilePanel = signal(false);
  selectedProfileIds: string[] = [];
  readonly availableProfiles = signal<{ id: string; code: string; name: string }[]>([]);

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
    });
  }

  openCreateForm(): void {
    this.editingId.set(null);
    this.formCode = '';
    this.formName = '';
    this.formDescription = '';
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  async saveProgram(): Promise<void> {
    if (this.editingId()) {
      const dto: AnalysisProgramUpdateDto = {
        code: this.formCode,
        name: this.formName,
        description: this.formDescription || null,
        isActive: true,
      };
      await this.store.update(this.editingId()!, dto);
    } else {
      const dto: AnalysisProgramAddDto = {
        code: this.formCode,
        name: this.formName,
        description: this.formDescription || null,
      };
      await this.store.create(dto);
    }
    this.closeForm();
  }

  async openManageProfiles(programId: string): Promise<void> {
    await this.store.loadById(programId);
    await this.profileStore.loadAll({ isActive: true });
    this.updateAvailableProfiles();
    this.selectedProfileIds = [];
    this.showProfilePanel.set(true);
  }

  closeProfilePanel(): void {
    this.showProfilePanel.set(false);
  }

  async addProfiles(): Promise<void> {
    const program = this.store.selectedProgram();
    if (program && this.selectedProfileIds.length > 0) {
      await this.store.addProfiles(program.id, this.selectedProfileIds);
      this.selectedProfileIds = [];
      this.updateAvailableProfiles();
    }
  }

  async removeProfile(programId: string, profileId: string): Promise<void> {
    await this.store.removeProfile(programId, profileId);
    this.updateAvailableProfiles();
  }

  private updateAvailableProfiles(): void {
    const program = this.store.selectedProgram();
    const allProfiles = this.profileStore.profiles();
    if (program) {
      const assignedIds = new Set(program.profiles.map(p => p.id));
      this.availableProfiles.set(
        allProfiles
          .filter(p => !assignedIds.has(p.id))
          .map(p => ({ id: p.id, code: p.code, name: p.name }))
      );
    }
  }
}
