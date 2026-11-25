import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SessionStore } from '@core/auth/session-store';
import { UnsavedChanges } from '@shared/forms/unsaved-changes';
import { HasUnsavedChanges } from '@shared/forms/unsaved-changes-guard';
import {
  communicationsTeam,
  hrTeam,
  memberUser,
  provideSessionTesting,
  seedSession,
} from '@testing/session';
import { vi } from 'vitest';
import { TeamSwitcher } from './team-switcher';

/** A page next to the switcher that may hold unsaved edits, like Team settings. */
@Component({
  imports: [TeamSwitcher],
  template: `<app-team-switcher />`,
})
class Host implements HasUnsavedChanges {
  readonly dirty = signal(false);

  constructor() {
    inject(UnsavedChanges).track(this, inject(DestroyRef));
  }

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }
}

describe('TeamSwitcher', () => {
  let fixture: ComponentFixture<Host>;
  let session: InstanceType<typeof SessionStore>;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideSessionTesting() });
    seedSession(memberUser, communicationsTeam.id);
    session = TestBed.inject(SessionStore);
    fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
  });

  function pick(teamId: string): void {
    const name = teamId === hrTeam.id ? hrTeam.name : communicationsTeam.name;

    [...fixture.nativeElement.querySelectorAll('[role=menuitemradio]')]
      .find((item: HTMLElement) => item.textContent?.includes(name))!
      .click();
  }

  it('switches straight away when nothing is unsaved', () => {
    const confirm = vi.spyOn(window, 'confirm');

    pick(hrTeam.id);

    expect(session.activeTeam()?.id).toBe(hrTeam.id);
    expect(confirm).not.toHaveBeenCalled();
  });

  it('keeps the team and the edits when the person declines to discard them', () => {
    fixture.componentInstance.dirty.set(true);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

    pick(hrTeam.id);

    expect(confirm).toHaveBeenCalledWith('You have unsaved changes. Switch team and discard them?');
    expect(session.activeTeam()?.id).toBe(communicationsTeam.id);
  });

  it('switches when the person agrees to discard the edits', () => {
    fixture.componentInstance.dirty.set(true);
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    pick(hrTeam.id);

    expect(session.activeTeam()?.id).toBe(hrTeam.id);
  });

  it('asks nothing when the active team is picked again', () => {
    fixture.componentInstance.dirty.set(true);
    const confirm = vi.spyOn(window, 'confirm');

    pick(communicationsTeam.id);

    expect(confirm).not.toHaveBeenCalled();
  });
});
