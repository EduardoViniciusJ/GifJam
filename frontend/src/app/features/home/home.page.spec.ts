import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { AuthService } from '@core/auth/auth.service';
import { SessionTokenService } from '@core/auth/session-token.service';
import { MatchmakingSnapshot } from '@features/matchmaking/data/matchmaking.models';

import { HomePage } from './home.page';

describe('HomePage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();

    TestBed.inject(SessionTokenService).clear();
  });

  it('normalizes the room code to five uppercase characters', async () => {
    const fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();

    const input = fixture.nativeElement.querySelector('#room-code') as HTMLInputElement;
    input.value = 'a-bc12x';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(input.value).toBe('ABC12');
  });

  it('shows validation when trying to enter an incomplete room code', async () => {
    const fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();

    fixture.componentInstance.joinRoom();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Digite os 5 caracteres da sala.');
  });

  it('navigates once to a valid normalized room code for an authenticated player', async () => {
    authenticate();
    const fixture = TestBed.createComponent(HomePage);
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.componentInstance.roomCode.setValue('ABC12');
    const firstJoin = fixture.componentInstance.joinRoom();
    const repeatedJoin = fixture.componentInstance.joinRoom();
    await Promise.all([firstJoin, repeatedJoin]);

    expect(navigate).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/sala', 'ABC12']);
  });

  it('starts login with the room return URL for a logged-out player', async () => {
    const fixture = TestBed.createComponent(HomePage);
    const auth = TestBed.inject(AuthService);
    const startLogin = vi.spyOn(auth, 'startDiscordLogin').mockImplementation(() => undefined);
    vi.spyOn(auth, 'restore').mockReturnValue(of(null));

    fixture.componentInstance.roomCode.setValue('ABC12');
    await fixture.componentInstance.joinRoom();

    expect(startLogin).toHaveBeenCalledWith('/sala/abc12');
  });

  it('hides the global ranking from logged-out visitors', async () => {
    const fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Como jogar');
    expect(text).not.toContain('Ranking');
    expect(text).not.toContain('Segurança');
    expect(text).not.toContain('Powered by KLIPY');
    expect(fixture.nativeElement.querySelector('.hero__mascot')).toBeNull();
  });

  it('shows the global ranking link to logged-in visitors', async () => {
    TestBed.inject(SessionTokenService).set('test-token', {
      id: 'user-id',
      discordId: 'discord-id',
      username: 'player',
      displayName: 'Player',
      avatarUrl: null,
    });

    const fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ranking');
  });

  it('shows the matchmaking button and starts login for logged-out visitors', async () => {
    const fixture = TestBed.createComponent(HomePage);
    const auth = TestBed.inject(AuthService);
    const startLogin = vi.spyOn(auth, 'startDiscordLogin').mockImplementation(() => undefined);
    vi.spyOn(auth, 'restore').mockReturnValue(of(null));
    await fixture.whenStable();
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.matchmaking-button') as HTMLButtonElement;
    expect(button.textContent).toContain('Entrar na fila');

    button.click();

    await vi.waitFor(() => expect(startLogin).toHaveBeenCalledWith('/'));
  });

  it('enters matchmaking directly for logged-in visitors', async () => {
    TestBed.inject(SessionTokenService).set('test-token', {
      id: 'user-id',
      discordId: 'discord-id',
      username: 'player',
      displayName: 'Player',
      avatarUrl: null,
    });

    const fixture = TestBed.createComponent(HomePage);
    const matchmaking = fixture.componentInstance.matchmaking;
    const toggleQueue = vi.spyOn(matchmaking, 'toggleQueue').mockResolvedValue();
    await fixture.whenStable();
    fixture.detectChanges();

    await fixture.componentInstance.enterMatchmaking();

    expect(toggleQueue).toHaveBeenCalledOnce();
  });

  it('restores the cookie session before room and matchmaking actions', async () => {
    const fixture = TestBed.createComponent(HomePage);
    const auth = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    const restoredUser = {
      id: 'user-id',
      discordId: 'discord-id',
      username: 'player',
      displayName: 'Player',
      avatarUrl: null,
    };
    vi.spyOn(auth, 'restore').mockReturnValue(of(restoredUser));
    const startLogin = vi.spyOn(auth, 'startDiscordLogin').mockImplementation(() => undefined);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const toggleQueue = vi
      .spyOn(fixture.componentInstance.matchmaking, 'toggleQueue')
      .mockResolvedValue();

    await fixture.componentInstance.createRoom();
    fixture.componentInstance.roomCode.setValue('ABC12');
    await fixture.componentInstance.joinRoom();
    await fixture.componentInstance.enterMatchmaking();

    expect(navigate).toHaveBeenCalledWith(['/sala', 'nova']);
    expect(navigate).toHaveBeenCalledWith(['/sala', 'ABC12']);
    expect(toggleQueue).toHaveBeenCalledOnce();
    expect(startLogin).not.toHaveBeenCalled();
  });

  it('shows the player count while waiting for another player', () => {
    authenticate();
    const fixture = TestBed.createComponent(HomePage);
    fixture.componentInstance.matchmaking.snapshot.set(waitingSnapshot(1, null));
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('.matchmaking-status') as HTMLElement;
    expect(status.textContent).toContain('1 jogador na fila');
    expect(status.textContent).toContain('Aguardando outro jogador');
  });

  it('shows the countdown after the second player joins', () => {
    authenticate();
    const fixture = TestBed.createComponent(HomePage);
    fixture.componentInstance.matchmaking.snapshot.set(
      waitingSnapshot(2, new Date(Date.now() + 30_000).toISOString()),
    );
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('.matchmaking-status') as HTMLElement;
    expect(status.textContent).toContain('2 jogadores na fila');
    expect(status.textContent).toMatch(/Partida em (30|31)s/);
  });
});

function authenticate(): void {
  TestBed.inject(SessionTokenService).set('test-token', {
    id: 'user-id',
    discordId: 'discord-id',
    username: 'player',
    displayName: 'Player',
    avatarUrl: null,
  });
}

function waitingSnapshot(playerCount: number, deadlineAt: string | null): MatchmakingSnapshot {
  return {
    status: 'Waiting',
    playerCount,
    minimumPlayers: 2,
    maximumPlayers: 6,
    hostUserId: 'user-id',
    deadlineAt,
    gameCode: null,
    gameMode: null,
    serverTime: new Date().toISOString(),
  };
}
