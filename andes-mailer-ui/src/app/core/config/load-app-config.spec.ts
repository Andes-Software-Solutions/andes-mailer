import { testAppConfig, testTelemetryConfig } from '@testing/app-config';
import { vi } from 'vitest';
import { loadAppConfig } from './load-app-config';

describe('loadAppConfig', () => {
  const baseUri = 'http://localhost:4200/';

  function respondWith(body: unknown, init?: ResponseInit): void {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(JSON.stringify(body), init))),
    );
  }

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns a valid config', async () => {
    respondWith(testAppConfig);

    await expect(loadAppConfig(baseUri)).resolves.toEqual(testAppConfig);
  });

  it('returns a config with a telemetry section', async () => {
    const config = { ...testAppConfig, telemetry: testTelemetryConfig };
    respondWith(config);

    await expect(loadAppConfig(baseUri)).resolves.toEqual(config);
  });

  it('resolves config.json against the base URI, so deep routes reload', async () => {
    respondWith(testAppConfig);

    await loadAppConfig('https://example.com/mailer/');

    expect(vi.mocked(fetch).mock.calls[0]?.[0]).toEqual(
      new URL('https://example.com/mailer/config.json'),
    );
  });

  it('rejects when the file is missing', async () => {
    respondWith({}, { status: 404 });

    await expect(loadAppConfig(baseUri)).rejects.toThrow('HTTP 404');
  });

  it.each([
    ['a missing auth section', {}, 'auth.clientId'],
    ['a client id that is not a GUID', { auth: { clientId: 'x' } }, 'auth.clientId'],
    [
      'an authority that is not https',
      { auth: { ...testAppConfig.auth, authority: 'http://login.microsoftonline.com/x' } },
      'auth.authority',
    ],
    ['no scopes', { auth: { ...testAppConfig.auth, apiScopes: [] } }, 'auth.apiScopes'],
    ['an empty scope', { auth: { ...testAppConfig.auth, apiScopes: [' '] } }, 'auth.apiScopes'],
    [
      'a scope that is not a string',
      { auth: { ...testAppConfig.auth, apiScopes: [42] } },
      'auth.apiScopes',
    ],
    [
      'a null telemetry section',
      { ...testAppConfig, telemetry: null },
      'telemetry.connectionString',
    ],
    [
      'a blank connection string',
      { ...testAppConfig, telemetry: { connectionString: ' ' } },
      'telemetry.connectionString',
    ],
    [
      'an instrumentation key on its own',
      { ...testAppConfig, telemetry: { connectionString: '00000000-0000-4000-8000-000000000000' } },
      'telemetry.connectionString',
    ],
  ])('rejects %s', async (_case, body, message) => {
    respondWith(body);

    await expect(loadAppConfig(baseUri)).rejects.toThrow(message);
  });
});
