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

  it('announces and focuses the failure when Microsoft cannot be reached', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: provideSessionTesting(
        signedInMsal({ loginRedirect: () => Promise.reject(new Error('endpoints_resolution')) }),
      ),
    });
    seedSession(noAccessUser);
    const fixture = TestBed.createComponent(NoAccess);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    const status = element.querySelector('[role=status]');
    element.querySelector('button')?.click();
    await vi.waitFor(async () => {
      await fixture.whenStable();
      expect(element.querySelector('[role=status]')).toBe(status);
      expect(status?.textContent).toContain("We couldn't reach Microsoft.");
      expect(document.activeElement).toBe(status?.querySelector('p'));
    });
    expect(element.querySelector('button')?.disabled).toBe(false);
  });
});
