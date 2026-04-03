import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AnalysisProgramApiService } from '../services/analysis-program-api.service';
import {
  AnalysisProgramDto,
  AnalysisProgramListDto,
  AnalysisProgramAddDto,
  AnalysisProgramUpdateDto,
  AnalysisProgramFilteringInputDto,
} from '../models/analysis-program.model';

@Injectable({ providedIn: 'root' })
export class AnalysisProgramDatastore {
  private readonly api = inject(AnalysisProgramApiService);

  readonly programs = signal<AnalysisProgramListDto[]>([]);
  readonly selectedProgram = signal<AnalysisProgramDto | null>(null);
  readonly loading = signal(false);

  async loadAll(filter?: AnalysisProgramFilteringInputDto): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getAll(filter));
      this.programs.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async loadById(id: string): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getById(id));
      this.selectedProgram.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: AnalysisProgramAddDto): Promise<AnalysisProgramDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadAll();
    return created;
  }

  async update(id: string, dto: AnalysisProgramUpdateDto): Promise<AnalysisProgramDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadAll();
    return updated;
  }

  async toggleStatus(id: string): Promise<AnalysisProgramDto> {
    const toggled = await firstValueFrom(this.api.toggleStatus(id));
    await this.loadAll();
    return toggled;
  }

  async addProfiles(id: string, profileIds: string[]): Promise<AnalysisProgramDto> {
    const updated = await firstValueFrom(this.api.addProfiles(id, { profileIds }));
    this.selectedProgram.set(updated);
    await this.loadAll();
    return updated;
  }

  async removeProfile(id: string, profileId: string): Promise<void> {
    await firstValueFrom(this.api.removeProfile(id, profileId));
    await this.loadById(id);
    await this.loadAll();
  }
}
