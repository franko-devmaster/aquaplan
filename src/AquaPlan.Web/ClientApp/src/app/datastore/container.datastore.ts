import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ContainerApiService } from '../services/container-api.service';
import {
  ContainerDto,
  ContainerListDto,
  ContainerAddDto,
  ContainerUpdateDto,
  ContainerFilteringInputDto,
} from '../models/container.model';

@Injectable({ providedIn: 'root' })
export class ContainerDatastore {
  private readonly api = inject(ContainerApiService);

  readonly containers = signal<ContainerListDto[]>([]);
  readonly loading = signal(false);

  async loadAll(filter?: ContainerFilteringInputDto): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getAll(filter));
      this.containers.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: ContainerAddDto): Promise<ContainerDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadAll();
    return created;
  }

  async update(id: string, dto: ContainerUpdateDto): Promise<ContainerDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadAll();
    return updated;
  }

  async toggleStatus(id: string): Promise<ContainerDto> {
    const toggled = await firstValueFrom(this.api.toggleStatus(id));
    await this.loadAll();
    return toggled;
  }
}
