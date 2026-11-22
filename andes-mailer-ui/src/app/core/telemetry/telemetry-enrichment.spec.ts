import type { ITelemetryItem } from '@microsoft/applicationinsights-web';
import { UI_ROLE_NAME, apiOperation, enrichTelemetry } from './telemetry-enrichment';

describe('enrichTelemetry', () => {
  function dependency(name: string, target: string): ITelemetryItem {
    return { name: 'dependency', baseType: 'RemoteDependencyData', baseData: { name, target } };
  }

  it('tags every item with the UI role', () => {
    const item: ITelemetryItem = { name: 'pageview', baseType: 'PageviewData', baseData: {} };

    enrichTelemetry(item, '/');

    expect(item.tags?.['ai.cloud.role']).toBe(UI_ROLE_NAME);
    expect(item.data).toBeUndefined();
  });

  it('tags an API call with the screen that made it and the operation it called', () => {
    const id = 'f1e2d3c4-b5a6-4978-8a9b-0c1d2e3f4a5b';
    const item = dependency(
      `POST /api/teams/${id}/emails/${id}/send`,
      `https://localhost/api/teams/${id}`,
    );

    enrichTelemetry(item, '/teams/:teamId');

    expect(item.data).toEqual({
      page: '/teams/:teamId',
      apiOperation: 'POST /api/teams/{id}/emails/{id}/send',
    });
  });

  it('drops the query string, which can hold what the user searched for', () => {
    const item = dependency(
      'GET /api/users/directory?search=ada@example.com',
      'https://localhost/api/users/directory?search=ada@example.com',
    );

    enrichTelemetry(item, '/');

    expect(item.baseData).toEqual({
      name: 'GET /api/users/directory',
      target: 'https://localhost/api/users/directory',
    });
    expect(JSON.stringify(item)).not.toContain('ada@example.com');
  });

  it('drops the query string from a dependency that carries its own data field', () => {
    const url = 'https://localhost/api/users/directory?search=ada@example.com';
    const item = dependency('GET /api/users/directory', url);
    item.baseData = { ...item.baseData, data: url };

    enrichTelemetry(item, '/');

    expect(item.baseData['data']).toBe('https://localhost/api/users/directory');
  });
});

describe('apiOperation', () => {
  it('keeps a call without ids as it is', () => {
    expect(apiOperation('GET /api/users/me')).toBe('GET /api/users/me');
  });
});
