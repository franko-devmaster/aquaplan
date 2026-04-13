import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UserApiService } from '../services/user-api.service';
import { UserListDto, UserDetailDto, UserCreateDto, UserUpdateDto } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserDatastore {
  private readonly api = inject(UserApiService);

  readonly users = signal<UserListDto[]>([]);
  readonly loading = signal(false);

  async loadAll(filter?: { role?: string; distributorId?: string; isActive?: boolean }): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.api.getAll(filter));
      this.users.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  async create(dto: UserCreateDto): Promise<UserDetailDto> {
    const created = await firstValueFrom(this.api.create(dto));
    await this.loadAll();
    return created;
  }

  async update(id: string, dto: UserUpdateDto): Promise<UserDetailDto> {
    const updated = await firstValueFrom(this.api.update(id, dto));
    await this.loadAll();
    return updated;
  }

  async deactivate(id: string): Promise<void> {
    await firstValueFrom(this.api.deactivate(id));
    await this.loadAll();
  }
}
