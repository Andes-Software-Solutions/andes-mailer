import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Permission } from '@core/api/permission.model';
import { provideTestAppConfig } from '@testing/app-config';
import { provideFakeMsal, signedInMsal } from '@testing/msal';
import { communicationsTeam, memberUser, seedSession } from '@testing/session';
import { HasPermission } from './has-permission';
import { PermissionMode, SessionStore } from './session-store';

@Component({
  imports: [HasPermission],
  template: `
    <span *appHasPermission="required(); mode: mode(); else: denied" id="granted">granted</span>
    <ng-template #denied><span id="denied">denied</span></ng-template>
  `,
})
class Host {
  readonly required = signal<Permission | readonly Permission[]>('ComposeTeamEmails');
  readonly mode = signal<PermissionMode>('any');
}

describe('HasPermission', () => {
  let fixture: ComponentFixture<Host>;

  function shown(id: string): boolean {
    return (fixture.nativeElement as HTMLElement).querySelector(`#${id}`) !== null;
  }

  beforeEach(async () => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(),
        provideFakeMsal(signedInMsal()),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    seedSession(memberUser);
    fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
  });

  it('renders for a permission the active team grants', () => {
    expect(shown('granted')).toBe(true);
    expect(shown('denied')).toBe(false);
  });

  it('renders the else template when the permission is missing', async () => {
    fixture.componentInstance.required.set('ModifyTeamUser');
    await fixture.whenStable();

    expect(shown('granted')).toBe(false);
    expect(shown('denied')).toBe(true);
  });

  it('needs every permission in all mode', async () => {
    fixture.componentInstance.required.set(['ComposeTeamEmails', 'ModifyTeamUser']);
    await fixture.whenStable();
    expect(shown('granted')).toBe(true);

    fixture.componentInstance.mode.set('all');
    await fixture.whenStable();
    expect(shown('granted')).toBe(false);
  });

  it('follows a team switch', async () => {
    fixture.componentInstance.required.set('ModifyTeamUser');
    await fixture.whenStable();

    TestBed.inject(SessionStore).selectTeam(communicationsTeam.id);
    await fixture.whenStable();

    expect(shown('granted')).toBe(true);
  });
});
