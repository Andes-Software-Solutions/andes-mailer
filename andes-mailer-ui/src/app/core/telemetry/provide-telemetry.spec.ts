import { HttpErrorResponse } from '@angular/common/http';
import { ApplicationInitStatus, Component, ErrorHandler } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { MsalBroadcastService } from '@azure/msal-angular';
import {
  EventMessage,
  EventType,
  IPublicClientApplication,
  InteractionStatus,
} from '@azure/msal-browser';
import { provideTestAppConfig, testAppConfig, testTelemetryConfig } from '@testing/app-config';
import {
  eventCallbacks,
  fakeAccount,
  fakeAuthResult,
  provideFakeMsal,
  signedInMsal,
  signedOutMsal,
} from '@testing/msal';
import {
  FakeTelemetryClient,
  fakeTelemetryClient,
  provideFakeTelemetryClient,
} from '@testing/telemetry';
import { filter, firstValueFrom } from 'rxjs';
import { vi } from 'vitest';
import { SIGNED_IN_EVENT, provideTelemetry } from './provide-telemetry';
import { Telemetry, TelemetryClient } from './telemetry';

@Component({ template: '' })
class Blank {}

describe('provideTelemetry', () => {
  const routes = [
    { path: '', title: 'Home', component: Blank },
    { path: 'teams/:teamId', title: 'Team', component: Blank },
  ];

  interface Setup {
    readonly instance?: IPublicClientApplication;
    readonly telemetry?: boolean;
    readonly load?: (client: FakeTelemetryClient) => Promise<TelemetryClient>;
  }

  async function setup({
    instance = signedInMsal(),
    telemetry = true,
    load,
  }: Setup = {}): Promise<FakeTelemetryClient> {
    const client = fakeTelemetryClient();

    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(
          telemetry ? { ...testAppConfig, telemetry: testTelemetryConfig } : testAppConfig,
        ),
        provideRouter(routes),
        provideFakeMsal(instance),
        provideTelemetry(),
        provideFakeTelemetryClient(() => (load ? load(client) : Promise.resolve(client))),
      ],
    });

    await TestBed.inject(ApplicationInitStatus).donePromise;
    await firstValueFrom(
      TestBed.inject(MsalBroadcastService).inProgress$.pipe(
        filter((status) => status === InteractionStatus.None),
      ),
    );

    return client;
  }

  function event(eventType: EventType, payload: EventMessage['payload']): EventMessage {
    return {
      eventType,
      interactionType: null,
      payload,
      error: null,
      correlationId: '',
      timestamp: 0,
    };
  }

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('never loads the SDK when config.json has no telemetry section', async () => {
    const load = vi.fn(() => Promise.resolve(fakeTelemetryClient()));

    await setup({ telemetry: false, load });

    expect(load).not.toHaveBeenCalled();
  });

  it('records a page view named after the route title, without the query string', async () => {
    const client = await setup();
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/teams/42?search=ada@example.com');

    expect(client.trackPageView).toHaveBeenLastCalledWith({
      name: 'Team',
      uri: new URL('teams/42', document.baseURI).href,
      properties: { route: '/teams/:teamId' },
    });
  });

  it('starts a new operation for each page view', async () => {
    const client = await setup();
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/teams/42');

    expect(client.installedTrace()).toEqual({
      traceId: expect.stringMatching(/^[0-9a-f]{32}$/),
      pageName: 'Team',
    });
  });

  it('tags API calls with the route on screen', async () => {
    const client = await setup();
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/teams/42');
    const item = {
      name: 'dependency',
      baseType: 'RemoteDependencyData',
      baseData: { name: 'GET /api/users/me' },
    };

    for (const initializer of client.initializers) {
      initializer(item);
    }

    expect(item).toMatchObject({
      data: { page: '/teams/:teamId', apiOperation: 'GET /api/users/me' },
    });
  });

  it('identifies a restored session by its object id only', async () => {
    const account = fakeAccount({ idTokenClaims: { oid: '0f9e8d7c-6b5a-4c3d-8e2f-1a0b9c8d7e6f' } });

    const client = await setup({ instance: signedInMsal({ getAllAccounts: () => [account] }) });

    expect(client.setAuthenticatedUserContext).toHaveBeenCalledWith(
      '0f9e8d7c-6b5a-4c3d-8e2f-1a0b9c8d7e6f',
      undefined,
      true,
    );
  });

  it('clears the user when nobody is signed in', async () => {
    const client = await setup({ instance: signedOutMsal() });

    expect(client.clearAuthenticatedUserContext).toHaveBeenCalled();
    expect(client.setAuthenticatedUserContext).not.toHaveBeenCalled();
  });

  it('records a sign-in and identifies the user who signed in', async () => {
    const { callbacks, addEventCallback } = eventCallbacks();
    const client = await setup({ instance: signedOutMsal({ addEventCallback }) });
    const signedIn = fakeAccount();

    for (const callback of callbacks) {
      callback(event(EventType.LOGIN_SUCCESS, fakeAuthResult({ account: signedIn })));
    }

    expect(client.trackEvent).toHaveBeenCalledWith({ name: SIGNED_IN_EVENT }, undefined);
    expect(client.setAuthenticatedUserContext).toHaveBeenCalledWith(
      signedIn.localAccountId,
      undefined,
      true,
    );
  });

  it('records a sign-in that completes while the SDK is still loading', async () => {
    const { callbacks, addEventCallback } = eventCallbacks();
    const signedIn = fakeAccount();

    const client = await setup({
      instance: signedOutMsal({ addEventCallback }),
      load: (fake) => {
        for (const callback of callbacks) {
          callback(event(EventType.LOGIN_SUCCESS, fakeAuthResult({ account: signedIn })));
        }

        return Promise.resolve(fake);
      },
    });

    expect(client.trackEvent).toHaveBeenCalledWith({ name: SIGNED_IN_EVENT }, undefined);
  });

  it('reports a failed API call by status and operation, without its query string', async () => {
    const client = await setup();
    const error = new HttpErrorResponse({
      status: 503,
      url: 'http://localhost/api/users/directory?search=ada@example.com',
    });

    TestBed.inject(Telemetry).trackException(error);

    const [[{ exception }]] = vi.mocked(client.trackException).mock.calls as [
      [{ exception: Error }],
    ];
    expect(exception.message).toBe('HTTP 503 http://localhost/api/users/directory');
  });

  it('starts the app without telemetry when the SDK cannot load', async () => {
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);

    await expect(
      setup({ load: () => Promise.reject(new Error('blocked by the network')) }),
    ).resolves.toBeDefined();

    expect(consoleWarn).toHaveBeenCalled();
  });

  it('reports errors Angular hands to its error handler', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const client = await setup();
    const error = new Error('boom');

    TestBed.inject(ErrorHandler).handleError(error);

    expect(client.trackException).toHaveBeenCalledWith({ exception: error });
    expect(consoleError).toHaveBeenCalledWith('ERROR', error);
  });
});
