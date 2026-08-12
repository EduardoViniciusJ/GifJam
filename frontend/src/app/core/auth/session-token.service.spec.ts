import { SessionTokenService } from './session-token.service';

describe('SessionTokenService', () => {
  it('starts without a client-side secret', () => {
    const service = new SessionTokenService();

    expect(service.user()).toBeNull();
    expect(service.get()).toBeNull();
  });
});
