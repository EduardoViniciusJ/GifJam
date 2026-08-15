import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '@core/auth/auth.service';

import { RoomDirectoryApiService } from '../data/room-directory-api.service';
import { PublicRoomSummary, RoomDirectorySort } from '../data/room-directory.models';
import { RoomDirectoryRealtimeService } from '../data/room-directory-realtime.service';

export interface RoomDirectoryOptions {
  pageSize: number;
  sort?: RoomDirectorySort;
}

@Injectable()
export class RoomDirectoryFacade {
  private readonly api = inject(RoomDirectoryApiService);
  private readonly realtime = inject(RoomDirectoryRealtimeService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private requestSequence = 0;

  readonly items = signal<PublicRoomSummary[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly total = signal(0);
  readonly sort = signal<RoomDirectorySort>('popular');
  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() * this.pageSize() < this.total());

  async initialize(options: RoomDirectoryOptions): Promise<void> {
    this.pageSize.set(options.pageSize);
    this.sort.set(options.sort ?? 'popular');
    this.page.set(1);
    await Promise.allSettled([this.refresh(), this.realtime.connect(() => this.scheduleRefresh())]);
  }

  async destroy(): Promise<void> {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }

    this.requestSequence++;
    await this.realtime.stop();
  }

  async refresh(): Promise<void> {
    const sequence = ++this.requestSequence;
    if (this.items().length === 0) {
      this.loading.set(true);
    }
    this.error.set(false);

    try {
      const response = await firstValueFrom(
        this.api.getPublic(this.sort(), this.page(), this.pageSize()),
      );
      if (sequence !== this.requestSequence) {
        return;
      }

      this.items.set(response.items);
      this.total.set(response.total);
      if (response.page > 1 && response.items.length === 0 && response.total > 0) {
        this.page.set(Math.max(1, response.page - 1));
        await this.refresh();
      }
    } catch {
      if (sequence === this.requestSequence) {
        this.error.set(true);
      }
    } finally {
      if (sequence === this.requestSequence) {
        this.loading.set(false);
      }
    }
  }

  async setSort(sort: RoomDirectorySort): Promise<void> {
    if (this.sort() === sort) {
      return;
    }

    this.sort.set(sort);
    this.page.set(1);
    await this.refresh();
  }

  async previousPage(): Promise<void> {
    if (!this.hasPreviousPage()) {
      return;
    }

    this.page.update((page) => page - 1);
    await this.refresh();
  }

  async nextPage(): Promise<void> {
    if (!this.hasNextPage()) {
      return;
    }

    this.page.update((page) => page + 1);
    await this.refresh();
  }

  async openRoom(room: PublicRoomSummary): Promise<void> {
    if (this.auth.isAuthenticated() || (await this.restoreSession())) {
      await this.router.navigate(['/sala', room.code]);
      return;
    }

    this.auth.startDiscordLogin(`/sala/${room.code.toLowerCase()}`);
  }

  private scheduleRefresh(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
    }

    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = null;
      void this.refresh();
    }, 300);
  }

  private async restoreSession(): Promise<boolean> {
    try {
      return Boolean(await firstValueFrom(this.auth.restore()));
    } catch {
      return false;
    }
  }
}
