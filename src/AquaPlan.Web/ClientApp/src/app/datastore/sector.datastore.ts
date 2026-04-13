import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SectorApiService } from '../services/sector-api.service';
import {
  SectorListDto,
  SectorAddDto,
  SectorUpdateDto,
  SectorFilteringInputDto,
  SectorDto,
} from '../models/sector.model';

@Injectable({ providedIn: 'root' })
export class SectorDatastore {
  private readonly api = inject(SectorApiService);

  readonly sectors = signal<SectorListDto[]>([]);
  readonly loading = signal(false);

  async loadAll(filter?: SectorFilteringInputDto): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getAll(filter));
      this.sectors.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: SectorAddDto): Promise<SectorDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadAll();
    return created;
  }

  async update(id: string, dto: SectorUpdateDto): Promise<SectorDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadAll();
    return updated;
  }

  async toggleStatus(id: string): Promise<SectorDto> {
    const toggled = await firstValueFrom(this.api.toggleStatus(id));
    await this.loadAll();
    return toggled;
  }
}
