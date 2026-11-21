import { ITelemetryItem } from '@microsoft/applicationinsights-web';

/** The UI's `cloud_RoleName`, beside the API's `andes-mailer-api`. */
export const UI_ROLE_NAME = 'andes-mailer-ui';

const DEPENDENCY_TYPE = 'RemoteDependencyData';

const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

/**
 * Tags every item with the UI's role, and each API call with the screen that made it and the
 * operation it called. Query strings are dropped from dependencies, because the directory search
 * puts what the user typed, often an email address, in one.
 */
export function enrichTelemetry(item: ITelemetryItem, page: string): void {
  item.tags ??= {};
  item.tags['ai.cloud.role'] = UI_ROLE_NAME;

  if (item.baseType !== DEPENDENCY_TYPE || !item.baseData) {
    return;
  }

  const dependency = item.baseData;

  if (typeof dependency['target'] === 'string') {
    dependency['target'] = withoutQuery(dependency['target']);
  }

  if (typeof dependency['name'] === 'string') {
    dependency['name'] = withoutQuery(dependency['name']);
    item.data = { ...item.data, page, apiOperation: apiOperation(dependency['name']) };
  }
}

/**
 * The call with its ids replaced, so every send of every draft groups as one operation:
 * `POST /api/teams/{id}/emails/{id}/send`.
 */
export function apiOperation(name: string): string {
  return withoutQuery(name).replace(GUID, '{id}');
}

/** The URL or dependency name up to its query string or fragment. */
export function withoutQuery(url: string): string {
  const end = url.search(/[?#]/);

  return end === -1 ? url : url.slice(0, end);
}
