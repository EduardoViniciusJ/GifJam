import { SessionTokenService } from './session-token.service';

describe('SessionTokenService', () => {
  it('starts without a client-side secret', () => {
    const service = new SessionTokenService();

    expect(service.user()).toBeNull();
    expect(service.get()).toBeNull();
  });

  it('keeps only the CSRF proof in memory and clears it on logout', () => {
    const service = new SessionTokenService();
    service.setSession(
      {
        id: 'user-id',
        discordId: 'discord-id',
        username: 'player',
        displayName: 'Player',
        avatarUrl: null,
      },
      'csrf-token',
    );

    expect(service.get()).toBeNull();
    expect(service.getCsrfToken()).toBe('csrf-token');

    service.clear();

    expect(service.getCsrfToken()).toBeNull();
  });
});
