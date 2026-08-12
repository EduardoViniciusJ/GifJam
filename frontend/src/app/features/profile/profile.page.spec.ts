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

  it('accepts a case-insensitive confirmation and returns home after deletion', () => {
    const fixture = TestBed.createComponent(ProfilePage);
    const auth = TestBed.inject(AuthService);
    const router = TestBed.inject(Router);
    const deleteAccount = vi.spyOn(auth, 'deleteAccount').mockReturnValue(of(void 0));
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    fixture.componentInstance.showDelete.set(true);
    fixture.componentInstance.confirmation.setValue('excluir');
    fixture.detectChanges();

    expect(fixture.componentInstance.confirmation.valid).toBe(true);

    fixture.componentInstance.deleteAccount();

    expect(deleteAccount).toHaveBeenCalledWith('excluir');
    expect(navigate).toHaveBeenCalledWith('/');
  });
});
