import { Injectable, inject, signal, computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { OrderApiService } from '../services/order-api.service';
import {
  OrderListDto,
  OrderDetailDto,
  OrderCreateDto,
  OrderUpdateDto,
  OrderAssignDto,
  OrderFilterDto,
  OrderPagedResultDto,
  OrderStatus,
  ResultsStatus,
} from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderDatastore {
  private readonly api = inject(OrderApiService);

  readonly orders = signal<OrderListDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);

  // Filter state
  readonly currentPage = signal(1);
  readonly pageSize = signal(20);
  readonly statusFilter = signal<OrderStatus[]>([]);
  readonly isUnassignedFilter = signal<boolean | undefined>(undefined);
  readonly searchFilter = signal('');
  readonly sortBy = signal<string | undefined>(undefined);
  readonly sortDescending = signal(true);
  readonly distributorFilter = signal<string | undefined>(undefined);
  readonly preleveurFilter = signal<string | undefined>(undefined);
  readonly dateFromFilter = signal<string | undefined>(undefined);
  readonly dateToFilter = signal<string | undefined>(undefined);
  readonly resultsStatusFilter = signal<ResultsStatus | undefined>(undefined);

  readonly totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  async loadFiltered(): Promise<void> {
    this.loading.set(true);
    try {
      const filter: OrderFilterDto = {
        statuses: this.statusFilter().length > 0 ? this.statusFilter() : undefined,
        isUnassigned: this.isUnassignedFilter(),
        search: this.searchFilter() || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize(),
        sortBy: this.sortBy(),
        sortDescending: this.sortDescending(),
        distributorId: this.distributorFilter(),
        preleveurId: this.preleveurFilter(),
        dateFrom: this.dateFromFilter(),
        dateTo: this.dateToFilter(),
        resultsStatus: this.resultsStatusFilter(),
      };
      const result = await firstValueFrom(this.api.getFiltered(filter));
      this.orders.set(result.items);
      this.totalCount.set(result.totalCount);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: OrderCreateDto): Promise<OrderDetailDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadFiltered();
    return created;
  }

  async update(id: string, dto: OrderUpdateDto): Promise<OrderDetailDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadFiltered();
    return updated;
  }

  async deleteOrder(id: string): Promise<void> {
    await firstValueFrom(this.api.delete(id));
    await this.loadFiltered();
  }

  async assignPreleveur(orderId: string, dto: OrderAssignDto): Promise<OrderDetailDto> {
    const updated = await firstValueFrom(this.api.assignPreleveur(orderId, dto));
    await this.loadFiltered();
    return updated;
  }

  setPage(page: number): void {
    this.currentPage.set(page);
    this.loadFiltered();
  }

  setStatusFilter(statuses: OrderStatus[]): void {
    this.statusFilter.set(statuses);
    this.currentPage.set(1);
    this.loadFiltered();
  }

  setUnassignedFilter(isUnassigned: boolean | undefined): void {
    this.isUnassignedFilter.set(isUnassigned);
    this.currentPage.set(1);
    this.loadFiltered();
  }

  setSearch(search: string): void {
    this.searchFilter.set(search);
    this.currentPage.set(1);
    this.loadFiltered();
  }

  setSort(sortBy: string): void {
    if (this.sortBy() === sortBy) {
      this.sortDescending.set(!this.sortDescending());
    } else {
      this.sortBy.set(sortBy);
      this.sortDescending.set(true);
    }
    this.loadFiltered();
  }

  setResultsStatusFilter(status: ResultsStatus | undefined): void {
    this.resultsStatusFilter.set(status);
    this.currentPage.set(1);
    this.loadFiltered();
  }
}
