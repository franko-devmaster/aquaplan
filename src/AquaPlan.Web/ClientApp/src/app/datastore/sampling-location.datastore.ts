import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SamplingLocationApiService } from '../services/sampling-location-api.service';
import {
  SamplingLocationDto,
  SamplingLocationCreateDto,
  SamplingLocationUpdateDto,
} from '../models/sampling-location.model';

@Injectable({ providedIn: 'root' })
export class SamplingLocationDatastore {
  private readonly api = inject(SamplingLocationApiService);

  readonly locations = signal<SamplingLocationDto[]>([]);
  readonly loading = signal(false);

  async loadAll(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getForCurrentUser());
      this.locations.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: SamplingLocationCreateDto): Promise<SamplingLocationDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadAll();
    return created;
  }

  async update(id: string, dto: SamplingLocationUpdateDto): Promise<SamplingLocationDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadAll();
    return updated;
  }
}
