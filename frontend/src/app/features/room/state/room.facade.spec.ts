import { Location } from '@angular/common';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { AuthService } from '@core/auth/auth.service';
import { GameApiService } from '@features/game/data/game-api.service';
import { GameRealtimeService } from '@features/game/data/game-realtime.service';
import { GameStore } from '@features/game/state/game.store';

import { RoomFacade } from './room.facade';

describe('RoomFacade', () => {
  it('leaves the current room, stops realtime and returns home', async () => {
    const leave = vi.fn(() => of(undefined));
    const stop = vi.fn().mockResolvedValue(undefined);
    const navigateByUrl = vi.fn().mockResolvedValue(true);

    TestBed.configureTestingModule({
      providers: [
        RoomFacade,
        GameStore,
        { provide: AuthService, useValue: { user: signal(null) } },
        { provide: GameApiService, useValue: { leave } },
        {
          provide: GameRealtimeService,
          useValue: { state: signal('connected'), stop },
        },
        { provide: Location, useValue: {} },
        { provide: Router, useValue: { navigateByUrl } },
      ],
    });

    const store = TestBed.inject(GameStore);
    store.setSnapshot({
      isHost: true,
      round: null,
      lobby: {
        code: 'ABC12',
        status: 'Lobby',
        mode: 'Classic',
        totalRounds: 3,
        phraseSubmissionSeconds: 60,
        resultsSeconds: 60,
        currentRoundNumber: 0,
        hostUserId: 'host-id',
        canStart: false,
        players: [],
        serverTime: new Date().toISOString(),
      },
    });

    const facade = TestBed.inject(RoomFacade);
    await facade.leaveRoom();

    expect(leave).toHaveBeenCalledWith('ABC12');
    expect(stop).toHaveBeenCalledOnce();
    expect(navigateByUrl).toHaveBeenCalledWith('/');
    expect(facade.leavingRoom()).toBe(false);
  });
});
