import { AppConfig } from './app-config.model';

/** Without a limit, a wedged host leaves a blank page instead of the startup failure message. */
const FETCH_TIMEOUT_MS = 10_000;

const GUID = /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;

export async function loadAppConfig(baseUri: string): Promise<AppConfig> {
  // Against the base URI, so a hard reload on a deep route still finds the file.
  const response = await fetch(new URL('config.json', baseUri), {
    // A cached config is how one environment ends up pointed at another.
    cache: 'no-store',
    credentials: 'omit',
    signal: AbortSignal.timeout(FETCH_TIMEOUT_MS),
  });

  if (!response.ok) {
    throw new Error(`The application configuration could not be loaded (HTTP ${response.status}).`);
  }

  const config: unknown = await response.json();
  assertAppConfig(config);

  return config;
}

function assertAppConfig(value: unknown): asserts value is AppConfig {
  const auth = (value as Partial<AppConfig> | null)?.auth;

  if (typeof auth?.clientId !== 'string' || !GUID.test(auth.clientId)) {
    throw new Error('auth.clientId must be a GUID.');
  }

  if (typeof auth.authority !== 'string' || !auth.authority.startsWith('https://')) {
    throw new Error('auth.authority must be an https URL.');
  }

  if (!Array.isArray(auth.apiScopes) || auth.apiScopes.length === 0) {
    throw new Error('auth.apiScopes must list at least one scope.');
  }
}
