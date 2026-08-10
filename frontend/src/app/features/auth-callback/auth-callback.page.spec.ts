import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { vi } from 'vitest';

import { SessionTokenService } from '@core/auth/session-token.service';

import { AuthCallbackPage } from './auth-callback.page';

describe('AuthCallbackPage', () => {
  const navigateByUrl = vi.fn().mockResolvedValue(true);

  beforeEach(async () => {
    sessionStorage.clear();
    navigateByUrl.mockClear();

    await TestBed.configureTestingModule({
      imports: [AuthCallbackPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap({ code: 'one-time-code', returnUrl: '/sala/ABC12' }),
            },
          },
        },
        { provide: Router, useValue: { navigateByUrl } },
      ],
    }).compileComponents();
  });

  it('exchanges the one-time code, stores the session and resumes the pending route', async () => {
    const fixture = TestBed.createComponent(AuthCallbackPage);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('/api/auth/exchange');
    expect(request.request.body).toEqual({ code: 'one-time-code' });
    request.flush({
      accessToken: 'jwt-token',
      expiresAt: '2026-08-10T20:00:00Z',
      user: {
        id: 'user-id',
        discordId: 'discord-id',
        username: 'edu',
        displayName: 'Edu',
        avatarUrl: null,
      },
    });

    await fixture.whenStable();

    expect(TestBed.inject(SessionTokenService).get()).toBe('jwt-token');
    expect(navigateByUrl).toHaveBeenCalledWith('/sala/ABC12', { replaceUrl: true });
    http.verify();
  });
});
