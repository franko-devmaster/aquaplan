import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DistributorApiService } from '../services/distributor-api.service';
import {
  DistributorListDto,
  DistributorAddDto,
  DistributorUpdateDto,
  DistributorFilteringInputDto,
  DistributorDto,
} from '../models/distributor.model';

@Injectable({ providedIn: 'root' })
export class DistributorDatastore {
  private readonly api = inject(DistributorApiService);

  readonly distributors = signal<DistributorListDto[]>([]);
  readonly loading = signal(false);

  async loadAll(filter?: DistributorFilteringInputDto): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getAll(filter));
      this.distributors.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: DistributorAddDto): Promise<DistributorDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadAll();
    return created;
  }

  async update(id: string, dto: DistributorUpdateDto): Promise<DistributorDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadAll();
    return updated;
  }

  async toggleStatus(id: string): Promise<DistributorDto> {
    const toggled = await firstValueFrom(this.api.toggleStatus(id));
    await this.loadAll();
    return toggled;
  }
}
