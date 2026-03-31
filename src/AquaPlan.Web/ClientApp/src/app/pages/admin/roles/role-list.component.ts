import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { RoleApiService } from '../../../services/role-api.service';
import { RoleDto, RoleWithPermissionsDto } from '../../../models/role.model';

@Component({
  selector: 'app-role-list',
  standalone: true,
  imports: [
    MatTableModule, MatExpansionModule, MatChipsModule,
    MatProgressSpinnerModule, MatIconModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <h2>{{ 'roles.title' | translate }}</h2>
    </div>

    @if (loading()) {
      <div class="loading-container">
        <mat-spinner diameter="40"></mat-spinner>
      </div>
    } @else {
      <mat-accordion>
        @for (role of roles(); track role.id) {
          <mat-expansion-panel (opened)="loadRoleDetails(role)">
            <mat-expansion-panel-header>
              <mat-panel-title>{{ role.name }}</mat-panel-title>
              <mat-panel-description>{{ role.description }}</mat-panel-description>
            </mat-expansion-panel-header>

            @if (roleDetails()[role.id]; as details) {
              <h4>{{ 'roles.permissions' | translate }}</h4>
              <div class="permissions-container">
                @for (perm of details.permissions; track perm.id) {
                  <mat-chip>
                    <mat-icon matChipAvatar>security</mat-icon>
                    {{ perm.name }}
                  </mat-chip>
                }
                @if (details.permissions.length === 0) {
                  <p class="no-data">{{ 'common.noData' | translate }}</p>
                }
              </div>
            } @else {
              <mat-spinner diameter="24"></mat-spinner>
            }
          </mat-expansion-panel>
        }
      </mat-accordion>

      @if (roles().length === 0) {
        <p class="no-data">{{ 'common.noData' | translate }}</p>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .no-data { text-align: center; padding: 24px; color: #666; }
    .permissions-container { display: flex; flex-wrap: wrap; gap: 8px; padding: 8px 0; }
    mat-chip { margin: 2px; }
  `],
})
export class RoleListComponent implements OnInit {
  private readonly roleApi = inject(RoleApiService);

  readonly roles = signal<RoleDto[]>([]);
  readonly roleDetails = signal<Record<string, RoleWithPermissionsDto>>({});
  readonly loading = signal(false);

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.roleApi.getAll());
      this.roles.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async loadRoleDetails(role: RoleDto): Promise<void> {
    if (this.roleDetails()[role.id]) return;
    const details = await firstValueFrom(this.roleApi.getById(role.id));
    this.roleDetails.update((prev) => ({ ...prev, [role.id]: details }));
  }
}
