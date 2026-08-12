import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { SessionTokenService } from './session-token.service';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let session: SessionTokenService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionTokenService);
    session.setSession(
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

  afterEach(() => http.verify());

  it('clears the in-memory session after the account is deleted', () => {
    auth.deleteAccount('excluir').subscribe();

    const request = http.expectOne('/api/auth/account');
    expect(request.request.method).toBe('DELETE');
    expect(request.request.body).toEqual({ confirmation: 'excluir' });
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(session.user()).toBeNull();
    expect(session.getCsrfToken()).toBeNull();
  });
});
