import { Injectable, inject, signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SamplingRoundApiService } from '../services/sampling-round-api.service';
import {
  SamplingRoundListDto,
  SamplingRoundDetailDto,
  SamplingRoundCreateDto,
  SamplingRoundUpdateDto,
  SamplingRoundAssignDto,
  SamplingRoundFilterDto,
  SamplingRoundStatus,
} from '../models/sampling-round.model';

@Injectable({ providedIn: 'root' })
export class SamplingRoundDatastore {
  private readonly api = inject(SamplingRoundApiService);

  readonly rounds = signal<SamplingRoundListDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);

  readonly currentPage = signal(1);
  readonly pageSize = signal(20);
  readonly statusFilter = signal<SamplingRoundStatus[]>([]);
  readonly searchFilter = signal('');
  readonly sortBy = signal<string | undefined>(undefined);
  readonly sortDescending = signal(true);

  readonly totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  async loadFiltered(): Promise<void> {
    this.loading.set(true);
    try {
      const filter: SamplingRoundFilterDto = {
        statuses: this.statusFilter().length > 0 ? this.statusFilter() : undefined,
        search: this.searchFilter() || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize(),
        sortBy: this.sortBy(),
        sortDescending: this.sortDescending(),
      };
      const result = await firstValueFrom(this.api.getFiltered(filter));
      this.rounds.set(result.items);
      this.totalCount.set(result.totalCount);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: SamplingRoundCreateDto): Promise<SamplingRoundDetailDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadFiltered();
    return created;
  }

  async update(id: string, dto: SamplingRoundUpdateDto): Promise<SamplingRoundDetailDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadFiltered();
    return updated;
  }

  async deleteRound(id: string): Promise<void> {
    await firstValueFrom(this.api.delete(id));
    await this.loadFiltered();
  }

  async assign(id: string, dto: SamplingRoundAssignDto): Promise<SamplingRoundDetailDto> {
    const result = await firstValueFrom(this.api.assign(id, dto));
    await this.loadFiltered();
    return result;
  }

  async cancel(id: string): Promise<SamplingRoundDetailDto> {
    const result = await firstValueFrom(this.api.cancel(id));
    await this.loadFiltered();
    return result;
  }

  setStatusFilter(statuses: SamplingRoundStatus[]): void {
    this.statusFilter.set(statuses);
    this.currentPage.set(1);
    this.loadFiltered();
  }

  setSearch(search: string): void {
    this.searchFilter.set(search);
    this.currentPage.set(1);
    this.loadFiltered();
  }
}
