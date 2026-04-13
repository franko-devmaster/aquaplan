import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatRadioModule } from '@angular/material/radio';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { UserApiService } from '../../services/user-api.service';
import { UserListDto } from '../../models/user.model';

export interface AssignSamplerDialogData {
  distributorId: string;
  distributorName: string;
}

@Component({
  selector: 'app-assign-sampler-dialog',
  standalone: true,
  imports: [
    FormsModule, MatButtonModule, MatDialogModule, MatRadioModule,
    MatTableModule, MatProgressSpinnerModule, TranslateModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'samplingRounds.assignSampler' | translate }}</h2>
    <p class="subtitle">{{ data.distributorName }}</p>

    <mat-dialog-content>
      @if (loading()) {
        <div class="loading-container">
          <mat-spinner diameter="30"></mat-spinner>
        </div>
      } @else if (preleveurs().length === 0) {
        <p class="no-data">{{ 'samplingRounds.noPreleveursAvailable' | translate }}</p>
      } @else {
        <mat-radio-group [(ngModel)]="selectedId" class="preleveur-list">
          @for (user of preleveurs(); track user.id) {
            <mat-radio-button [value]="user.id" class="preleveur-item">
              <span class="preleveur-id">{{ user.userNumber }}</span>
              <span class="preleveur-name">{{ user.lastName }}, {{ user.firstName }}</span>
              <span class="preleveur-email">{{ user.email }}</span>
            </mat-radio-button>
          }
        </mat-radio-group>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-stroked-button mat-dialog-close>{{ 'common.cancel' | translate }}</button>
      <button mat-raised-button color="primary" (click)="assign()" [disabled]="!selectedId">
        {{ 'samplingRounds.assignSampler' | translate }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .subtitle { margin: -8px 24px 8px; color: #666; font-size: 14px; }
    .loading-container { display: flex; justify-content: center; padding: 24px; }
    .no-data { text-align: center; color: #666; padding: 24px; }
    .preleveur-list { display: flex; flex-direction: column; gap: 8px; min-width: 400px; }
    .preleveur-item { padding: 8px; border: 1px solid #e0e0e0; border-radius: 4px; }
    .preleveur-id { font-family: monospace; color: #999; margin-right: 8px; }
    .preleveur-name { font-weight: 500; margin-right: 12px; }
    .preleveur-email { color: #666; font-size: 13px; }
  `],
})
export class AssignSamplerDialogComponent implements OnInit {
  readonly data = inject<AssignSamplerDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<AssignSamplerDialogComponent>);
  private readonly userApi = inject(UserApiService);

  readonly loading = signal(true);
  readonly preleveurs = signal<UserListDto[]>([]);
  selectedId: string | null = null;

  async ngOnInit(): Promise<void> {
    try {
      const users = await firstValueFrom(this.userApi.getPreleveurs(this.data.distributorId));
      this.preleveurs.set(users);
    } finally {
      this.loading.set(false);
    }
  }

  assign(): void {
    if (this.selectedId) {
      this.dialogRef.close(this.selectedId);
    }
  }
}
