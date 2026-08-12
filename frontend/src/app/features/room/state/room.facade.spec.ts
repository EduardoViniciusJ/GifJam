import { Location } from '@angular/common';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { AuthService } from '@core/auth/auth.service';
import { GameApiService } from '@features/game/data/game-api.service';
import {
  GameRealtimeHandlers,
  GameRealtimeService,
} from '@features/game/data/game-realtime.service';
import { GameStore } from '@features/game/state/game.store';
import { RankingApiService } from '@features/ranking/data/ranking-api.service';

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
          provide: RankingApiService,
          useValue: { getGlobal: () => of({ entries: [], serverTime: new Date().toISOString() }) },
        },
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

  it('does not request another sync after a rate-limit rejection', async () => {
    const user = {
      id: 'host-id',
      discordId: 'discord-id',
      username: 'mitia',
      displayName: 'Mitia',
      avatarUrl: null,
    };
    const snapshot = {
      isHost: true,
      round: null,
      lobby: {
        code: 'ABC12',
        status: 'Lobby' as const,
        mode: 'Classic' as const,
        totalRounds: 3,
        phraseSubmissionSeconds: 60,
        resultsSeconds: 60,
        currentRoundNumber: 0,
        hostUserId: user.id,
        canStart: false,
        players: [
          {
            userId: user.id,
            username: user.username,
            displayName: user.displayName,
            avatarUrl: null,
            score: 0,
            isReady: true,
            isConnected: true,
            isHost: true,
          },
        ],
        serverTime: new Date().toISOString(),
      },
    };
    let realtimeHandlers!: GameRealtimeHandlers;
    const requestSync = vi.fn().mockResolvedValue(undefined);
    const stop = vi.fn().mockResolvedValue(undefined);
    const connect = vi.fn((_code: string, handlers: GameRealtimeHandlers) => {
      realtimeHandlers = handlers;
      return Promise.resolve();
    });

    TestBed.configureTestingModule({
      providers: [
        RoomFacade,
        GameStore,
        { provide: AuthService, useValue: { user: signal(user), restore: () => of(user) } },
        {
          provide: GameApiService,
          useValue: { join: () => of(snapshot) },
        },
        {
          provide: RankingApiService,
          useValue: {
            getGlobal: () =>
              of({
                entries: [
                  {
                    position: 3,
                    userId: user.id,
                    username: user.username,
                    displayName: user.displayName,
                    avatarUrl: null,
                    score: 120,
                    isCurrentUser: true,
                  },
                ],
                serverTime: new Date().toISOString(),
              }),
          },
        },
        {
          provide: GameRealtimeService,
          useValue: { state: signal('connected'), connect, requestSync, stop },
        },
        { provide: Location, useValue: {} },
        { provide: Router, useValue: {} },
      ],
    });

    const facade = TestBed.inject(RoomFacade);
    await facade.initialize('ABC12');
    await vi.waitFor(() => expect(facade.globalRankingStatus()).toBe('ready'));

    expect(facade.globalRanking()?.entries[0]?.score).toBe(120);

    realtimeHandlers.commandRejected({
      code: 'rate_limited',
      message: 'Você está fazendo ações rápido demais.',
    });

    expect(requestSync).not.toHaveBeenCalled();
    expect(facade.actionMessage()).toContain('rápido demais');

    await facade.destroy();
  });
});
