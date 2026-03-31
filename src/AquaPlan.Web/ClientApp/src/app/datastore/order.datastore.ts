import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { OrderApiService } from '../services/order-api.service';
import { OrderListDto, OrderDetailDto, OrderCreateDto, OrderAssignDto } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderDatastore {
  private readonly api = inject(OrderApiService);

  readonly orders = signal<OrderListDto[]>([]);
  readonly loading = signal(false);

  async loadAll(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getAll());
      this.orders.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: OrderCreateDto): Promise<OrderDetailDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadAll();
    return created;
  }

  async assignPreleveur(orderId: string, dto: OrderAssignDto): Promise<OrderDetailDto> {
    const updated = await firstValueFrom(this.api.assignPreleveur(orderId, dto));
    await this.loadAll();
    return updated;
  }
}
