import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { HasUnsavedChanges, unsavedChangesGuard } from './unsaved-changes-guard';

@Component({ template: 'form' })
class FormPage implements HasUnsavedChanges {
  readonly dirty = signal(false);

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }
}

@Component({ template: 'other' })
class OtherPage {}

describe('unsavedChangesGuard', () => {
  async function openForm(): Promise<{ harness: RouterTestingHarness; page: FormPage }> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'form', component: FormPage, canDeactivate: [unsavedChangesGuard] },
          { path: 'other', component: OtherPage },
        ]),
      ],
    });
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl('/form', FormPage);

    return { harness, page };
  }

  afterEach(() => vi.restoreAllMocks());

  it('lets a page without changes go without asking', async () => {
    const confirm = vi.spyOn(window, 'confirm');
    const { harness } = await openForm();

    await harness.navigateByUrl('/other');

    expect(confirm).not.toHaveBeenCalled();
    expect(TestBed.inject(Router).url).toBe('/other');
  });

  it('stays when the person keeps their changes', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { harness, page } = await openForm();
    page.dirty.set(true);

    await harness.navigateByUrl('/other');

    expect(TestBed.inject(Router).url).toBe('/form');
  });

  it('leaves when the person discards their changes', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { harness, page } = await openForm();
    page.dirty.set(true);

    await harness.navigateByUrl('/other');

    expect(TestBed.inject(Router).url).toBe('/other');
  });
});
