import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AnalysisProfileApiService } from '../services/analysis-profile-api.service';
import {
  AnalysisProfileDto,
  AnalysisProfileListDto,
  AnalysisProfileAddDto,
  AnalysisProfileUpdateDto,
  AnalysisProfileFilteringInputDto,
} from '../models/analysis-profile.model';

@Injectable({ providedIn: 'root' })
export class AnalysisProfileDatastore {
  private readonly api = inject(AnalysisProfileApiService);

  readonly profiles = signal<AnalysisProfileListDto[]>([]);
  readonly loading = signal(false);

  async loadAll(filter?: AnalysisProfileFilteringInputDto): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getAll(filter));
      this.profiles.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: AnalysisProfileAddDto): Promise<AnalysisProfileDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadAll();
    return created;
  }

  async update(id: string, dto: AnalysisProfileUpdateDto): Promise<AnalysisProfileDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadAll();
    return updated;
  }

  async toggleStatus(id: string): Promise<AnalysisProfileDto> {
    const toggled = await firstValueFrom(this.api.toggleStatus(id));
    await this.loadAll();
    return toggled;
  }
}
