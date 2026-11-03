import { vi } from 'vitest';
import { completeRedirect, isRedirectUrl, redirectUri } from './redirect-bridge';

describe('redirect bridge', () => {
  const baseUri = 'http://localhost:4200/';

  describe('redirectUri', () => {
    it('is the exact URI on the app registration', () => {
      expect(redirectUri(baseUri)).toBe('http://localhost:4200/auth');
    });

    it('follows a sub-path deployment', () => {
      expect(redirectUri('https://example.com/mailer/')).toBe('https://example.com/mailer/auth');
    });
  });

  describe('isRedirectUrl', () => {
    it('matches the redirect path, whatever the response in its hash', () => {
      expect(isRedirectUrl('http://localhost:4200/auth#code=abc&state=def', baseUri)).toBe(true);
    });

    it.each(['http://localhost:4200/', 'http://localhost:4200/authors'])(
      'does not match %s',
      (url) => {
        expect(isRedirectUrl(url, baseUri)).toBe(false);
      },
    );
  });

  describe('completeRedirect', () => {
    beforeEach(() => {
      vi.spyOn(console, 'error').mockImplementation(() => undefined);
    });

    afterEach(() => {
      vi.restoreAllMocks();
    });

    it('leaves navigation to the bridge when it succeeds', async () => {
      const location = { replace: vi.fn() };

      await completeRedirect(location, baseUri, () => Promise.resolve());

      expect(location.replace).not.toHaveBeenCalled();
    });

    it('goes to the failure page when the URL carries no response', async () => {
      const location = { replace: vi.fn() };

      await completeRedirect(location, baseUri, () => Promise.reject(new Error('no_payload')));

      expect(location.replace).toHaveBeenCalledWith('http://localhost:4200/login-failed');
    });
  });
});
