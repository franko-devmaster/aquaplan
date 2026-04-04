import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ChangeRequestApiService } from '../services/change-request-api.service';
import {
  ChangeRequestDto,
  ChangeRequestCreateDto,
  ChangeRequestUpdateDto,
} from '../models/change-request.model';

@Injectable({ providedIn: 'root' })
export class ChangeRequestDatastore {
  private readonly api = inject(ChangeRequestApiService);

  readonly myRequests = signal<ChangeRequestDto[]>([]);
  readonly pendingRequests = signal<ChangeRequestDto[]>([]);
  readonly loading = signal(false);

  async loadMyRequests(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getMyRequests());
      this.myRequests.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async loadPendingRequests(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getPendingRequests());
      this.pendingRequests.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async submitCreate(dto: ChangeRequestCreateDto): Promise<ChangeRequestDto> {
    const result = await firstValueFrom(this.api.submitCreate(dto));
    await this.loadMyRequests();
    return result;
  }

  async submitUpdate(slId: string, dto: ChangeRequestUpdateDto): Promise<ChangeRequestDto> {
    const result = await firstValueFrom(this.api.submitUpdate(slId, dto));
    await this.loadMyRequests();
    return result;
  }

  async submitDeactivate(slId: string): Promise<ChangeRequestDto> {
    const result = await firstValueFrom(this.api.submitDeactivate(slId));
    await this.loadMyRequests();
    return result;
  }

  async approve(id: string, comment: string | null): Promise<ChangeRequestDto> {
    const result = await firstValueFrom(this.api.approve(id, { comment }));
    await this.loadPendingRequests();
    return result;
  }

  async reject(id: string, comment: string): Promise<ChangeRequestDto> {
    const result = await firstValueFrom(this.api.reject(id, { comment }));
    await this.loadPendingRequests();
    return result;
  }
}
