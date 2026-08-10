import { SessionTokenService } from './session-token.service';

describe('SessionTokenService', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('discards malformed persisted user data', () => {
    sessionStorage.setItem('gifjam.session.user', JSON.stringify({ id: 'missing-fields' }));

    const service = new SessionTokenService();

    expect(service.user()).toBeNull();
    expect(sessionStorage.getItem('gifjam.session.user')).toBeNull();
  });

  it('normalizes and keeps a non-empty token', () => {
    const service = new SessionTokenService();

    service.set('  jwt-token  ');

    expect(service.get()).toBe('jwt-token');
    expect(service.isAuthenticated()).toBe(true);
  });
});
