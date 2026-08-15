import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '@core/auth/auth.service';

import { RoomDirectoryApiService } from '../data/room-directory-api.service';
import { PublicRoomDirectoryResponse, PublicRoomSummary } from '../data/room-directory.models';
import { RoomDirectoryRealtimeService } from '../data/room-directory-realtime.service';
import { RoomDirectoryFacade } from './room-directory.facade';

describe('RoomDirectoryFacade', () => {
  const room = createRoom('ABC12');
  const response: PublicRoomDirectoryResponse = {
    items: [room],
    page: 1,
    pageSize: 5,
    total: 1,
    serverTime: new Date().toISOString(),
  };
  let directoryChanged: (() => void) | null;
  const api = {
    getPublic: vi.fn(() => of(response)),
  };
  const realtime = {
    connect: vi.fn(async (handler: () => void) => {
      directoryChanged = handler;
    }),
    stop: vi.fn(async () => undefined),
  };
  const auth = {
    isAuthenticated: vi.fn(() => false),
    restore: vi.fn(() => of(null)),
    startDiscordLogin: vi.fn(),
  };
  const router = {
    navigate: vi.fn(async () => true),
  };

  beforeEach(() => {
    directoryChanged = null;
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        RoomDirectoryFacade,
        { provide: RoomDirectoryApiService, useValue: api },
        { provide: RoomDirectoryRealtimeService, useValue: realtime },
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  it('loads the public directory and coalesces realtime invalidations', async () => {
    vi.useFakeTimers();
    const facade = TestBed.inject(RoomDirectoryFacade);
    await facade.initialize({ pageSize: 5, sort: 'popular' });

    expect(facade.items()).toEqual([room]);
    expect(api.getPublic).toHaveBeenCalledOnce();
    directoryChanged?.();
    directoryChanged?.();
    directoryChanged?.();
    await vi.advanceTimersByTimeAsync(300);

    expect(api.getPublic).toHaveBeenCalledTimes(2);
  });

  it('starts Discord login with the selected room for anonymous visitors', async () => {
    const facade = TestBed.inject(RoomDirectoryFacade);

    await facade.openRoom(room);

    expect(auth.startDiscordLogin).toHaveBeenCalledWith('/sala/abc12');
    expect(router.navigate).not.toHaveBeenCalled();
  });
});

function createRoom(code: string): PublicRoomSummary {
  return {
    code,
    mode: 'Classic',
    totalRounds: 3,
    hostDisplayName: 'Player',
    hostAvatarUrl: null,
    playerCount: 2,
    capacity: 6,
    createdAt: new Date().toISOString(),
  };
}
