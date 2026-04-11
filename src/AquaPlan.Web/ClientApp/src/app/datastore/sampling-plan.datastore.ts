import { Injectable, inject, signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SamplingPlanApiService } from '../services/sampling-plan-api.service';
import {
  SamplingPlanListDto,
  SamplingPlanDetailDto,
  SamplingPlanCreateDto,
  SamplingPlanUpdateDto,
  SamplingPlanFilterDto,
  SamplingPlanStatus,
} from '../models/sampling-plan.model';

@Injectable({ providedIn: 'root' })
export class SamplingPlanDatastore {
  private readonly api = inject(SamplingPlanApiService);

  readonly plans = signal<SamplingPlanListDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);

  readonly currentPage = signal(1);
  readonly pageSize = signal(20);
  readonly statusFilter = signal<SamplingPlanStatus[]>([]);
  readonly yearFilter = signal<number | undefined>(undefined);
  readonly searchFilter = signal('');
  readonly sortBy = signal<string | undefined>(undefined);
  readonly sortDescending = signal(true);
  readonly distributorFilter = signal<string | undefined>(undefined);

  readonly totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  async loadFiltered(): Promise<void> {
    this.loading.set(true);
    try {
      const filter: SamplingPlanFilterDto = {
        statuses: this.statusFilter().length > 0 ? this.statusFilter() : undefined,
        search: this.searchFilter() || undefined,
        year: this.yearFilter(),
        distributorId: this.distributorFilter(),
        page: this.currentPage(),
        pageSize: this.pageSize(),
        sortBy: this.sortBy(),
        sortDescending: this.sortDescending(),
      };
      const result = await firstValueFrom(this.api.getFiltered(filter));
      this.plans.set(result.items);
      this.totalCount.set(result.totalCount);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: SamplingPlanCreateDto): Promise<SamplingPlanDetailDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadFiltered();
    return created;
  }

  async update(id: string, dto: SamplingPlanUpdateDto): Promise<SamplingPlanDetailDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadFiltered();
    return updated;
  }

  async deletePlan(id: string): Promise<void> {
    await firstValueFrom(this.api.delete(id));
    await this.loadFiltered();
  }

  async submit(id: string): Promise<SamplingPlanDetailDto> {
    const result = await firstValueFrom(this.api.submit(id));
    await this.loadFiltered();
    return result;
  }

  setStatusFilter(statuses: SamplingPlanStatus[]): void {
    this.statusFilter.set(statuses);
    this.currentPage.set(1);
    this.loadFiltered();
  }

  setYearFilter(year: number | undefined): void {
    this.yearFilter.set(year);
    this.currentPage.set(1);
    this.loadFiltered();
  }

  setSearch(search: string): void {
    this.searchFilter.set(search);
    this.currentPage.set(1);
    this.loadFiltered();
  }
}
