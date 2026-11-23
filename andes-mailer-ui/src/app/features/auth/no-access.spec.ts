import { TestBed } from '@angular/core/testing';
import { signedInMsal } from '@testing/msal';
import { noAccessUser, provideSessionTesting, seedSession } from '@testing/session';
import { vi } from 'vitest';
import { NoAccess } from './no-access';

describe('NoAccess', () => {
  it('names the account that has no access', async () => {
    TestBed.configureTestingModule({ providers: provideSessionTesting() });
    seedSession(noAccessUser);

    const fixture = TestBed.createComponent(NoAccess);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      `You're signed in as ${noAccessUser.email}.`,
    );
  });

  it('lets the person pick a different account', async () => {
    const loginRedirect = vi.fn(() => Promise.resolve());
    TestBed.configureTestingModule({
      providers: provideSessionTesting(signedInMsal({ loginRedirect })),
    });
    seedSession(noAccessUser);
    const fixture = TestBed.createComponent(NoAccess);
    await fixture.whenStable();

    (fixture.nativeElement as HTMLElement).querySelector('button')?.click();
    await fixture.whenStable();

    expect(loginRedirect).toHaveBeenCalledWith(
      expect.objectContaining({ prompt: 'select_account' }),
    );
  });
});
