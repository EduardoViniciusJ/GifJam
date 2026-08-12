import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { AuthService } from '@core/auth/auth.service';
import { SessionTokenService } from '@core/auth/session-token.service';

import { ProfilePage } from './profile.page';

describe('ProfilePage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProfilePage],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();

    TestBed.inject(SessionTokenService).setSession(
      {
        id: 'user-id',
        discordId: 'discord-id',
        username: 'player',
        displayName: 'Player',
        avatarUrl: null,
      },
      'csrf-token',
    );
  });

  it('sends the deletion request and returns home when the dialog is confirmed', () => {
    const fixture = TestBed.createComponent(ProfilePage);
    const auth = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    const deleteAccount = vi.spyOn(auth, 'deleteAccount').mockReturnValue(of(void 0));
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture.componentInstance.openDeleteDialog();
    fixture.componentInstance.deleteAccount();

    expect(fixture.componentInstance.showDelete()).toBe(true);
    expect(deleteAccount).toHaveBeenCalledWith('EXCLUIR');
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('closes the dialog without making a request when cancelled', () => {
    const fixture = TestBed.createComponent(ProfilePage);
    const auth = TestBed.inject(AuthService);
    const deleteAccount = vi.spyOn(auth, 'deleteAccount');

    fixture.componentInstance.openDeleteDialog();
    fixture.componentInstance.closeDeleteDialog();

    expect(fixture.componentInstance.showDelete()).toBe(false);
    expect(deleteAccount).not.toHaveBeenCalled();
  });
});
