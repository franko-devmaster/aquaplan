import { Injectable, inject, signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SamplingLocationApiService } from '../services/sampling-location-api.service';
import {
  SamplingLocationDto,
  SamplingLocationCreateDto,
  SamplingLocationUpdateDto,
  SamplingLocationListDto,
  SamplingLocationFilteringInputDto,
  ToggleStatusResultDto,
} from '../models/sampling-location.model';

export interface DistributorOption {
  id: string;
  name: string;
}

@Injectable({ providedIn: 'root' })
export class SamplingLocationDatastore {
  private readonly api = inject(SamplingLocationApiService);

  readonly locations = signal<SamplingLocationDto[]>([]);
  readonly filteredResult = signal<SamplingLocationListDto | null>(null);
  readonly loading = signal(false);

  readonly distributors = computed<DistributorOption[]>(() => {
    const locs = this.locations();
    const map = new Map<string, string>();
    for (const loc of locs) {
      if (!map.has(loc.distributorId)) {
        map.set(loc.distributorId, loc.distributorName ?? '');
      }
    }
    return Array.from(map, ([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });

  // F-007 — out-of-order response guard shared by loadAll/loadFiltered (see OrderDatastore).
  private requestSeq = 0;

  async loadAll(): Promise<void> {
    const reqId = ++this.requestSeq;
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getForCurrentUser());
      if (reqId !== this.requestSeq) {
        return;
      }
      this.locations.set(data);
    } finally {
      if (reqId === this.requestSeq) {
        this.loading.set(false);
      }
    }
  }

  async loadFiltered(filter: SamplingLocationFilteringInputDto): Promise<void> {
    const reqId = ++this.requestSeq;
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getFiltered(filter));
      if (reqId !== this.requestSeq) {
        return;
      }
      this.filteredResult.set(data);
    } finally {
      if (reqId === this.requestSeq) {
        this.loading.set(false);
      }
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

  async toggleStatus(id: string): Promise<ToggleStatusResultDto> {
    const result = await firstValueFrom(this.api.toggleStatus(id));
    await this.loadAll();
    return result;
  }
}
