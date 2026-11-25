import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import {
  FormField,
  disabled,
  email,
  form,
  max,
  maxLength,
  min,
  required,
  submit,
} from '@angular/forms/signals';
import { Router } from '@angular/router';
import { DirectoryApi } from '@core/api/directory-api';
import { Team, TeamColor, teamColorLabels, teamColors } from '@core/api/team.model';
import { routePaths } from '@core/navigation/route-paths';
import { ConfirmDialog } from '@shared/dialog/confirm-dialog';
import { EmailAutocomplete, EmailSuggester } from '@shared/email-autocomplete/email-autocomplete';
import { HasUnsavedChanges } from '@shared/forms/unsaved-changes-guard';
import { RichTextEditor } from '@shared/rich-text/rich-text-editor';
import { TeamAvatar } from '@shared/team-avatar/team-avatar';
import { TeamSettingsStore } from './team-settings-store';

/** The form's shape: every text field is a string, so an empty one means "none". */
interface SettingsModel {
  name: string;
  description: string;
  serviceAccountEmail: string;
  avatarColor: TeamColor;
  defaultSignature: string;
  confirmRecipientsAbove: number;
}

/** Mirrors the API's limits (`TeamLimits`). */
const NAME_MAX = 100;
const DESCRIPTION_MAX = 500;
const EMAIL_MAX = 256;
const SIGNATURE_MAX = 4000;
const RECIPIENTS_MIN = 1;
const RECIPIENTS_MAX = 500;

function toModel(team: Team | null): SettingsModel {
  return {
    name: team?.name ?? '',
    description: team?.description ?? '',
    serviceAccountEmail: team?.serviceAccountEmail ?? '',
    avatarColor: team?.avatarColor ?? 'Mint',
    defaultSignature: team?.defaultSignature ?? '',
    confirmRecipientsAbove: team?.confirmRecipientsAbove ?? 50,
  };
}

function sameModel(a: SettingsModel, b: SettingsModel): boolean {
  return (Object.keys(a) as (keyof SettingsModel)[]).every((key) => a[key] === b[key]);
}

/** The active team's settings: general details, sending defaults, safety rules, and leaving it. */
@Component({
  selector: 'app-team-settings',
  imports: [ConfirmDialog, EmailAutocomplete, FormField, RichTextEditor, TeamAvatar],
  providers: [TeamSettingsStore],
  styleUrl: './team-settings.scss',
  templateUrl: './team-settings.html',
  host: { class: 'd-flex flex-column gap-4' },
})
export class TeamSettings implements HasUnsavedChanges {
  protected readonly store = inject(TeamSettingsStore);
  private readonly _router = inject(Router);
  private readonly _directory = inject(DirectoryApi);

  protected readonly suggestMailbox: EmailSuggester = (query) =>
    this._directory.suggestEmails(query);

  protected readonly colors = teamColors;
  protected readonly colorLabels = teamColorLabels;
  protected readonly confirmingLeave = signal(false);
  /** Someone else saved the team since it loaded; saving again would only repeat the 409. */
  protected readonly conflict = linkedSignal({
    source: () => this.store.team(),
    computation: () => false,
  });

  /** What was last loaded or saved; the form edits a copy and resets when the team changes. */
  private readonly _saved = computed(() => toModel(this.store.team()));
  protected readonly model = linkedSignal(() => this._saved());

  protected readonly form = form(this.model, (path) => {
    required(path.name, { message: 'Enter a team name.' });
    maxLength(path.name, NAME_MAX, { message: `Use ${NAME_MAX} characters or fewer.` });
    maxLength(path.description, DESCRIPTION_MAX, {
      message: `Use ${DESCRIPTION_MAX} characters or fewer.`,
    });
    required(path.serviceAccountEmail, { message: 'Enter the mailbox the team sends from.' });
    email(path.serviceAccountEmail, { message: 'Enter a valid email address.' });
    maxLength(path.serviceAccountEmail, EMAIL_MAX);
    disabled(path.serviceAccountEmail, () => !this.store.canEditMailbox() || !this.store.canEdit());
    maxLength(path.defaultSignature, SIGNATURE_MAX, { message: 'The signature is too long.' });
    required(path.confirmRecipientsAbove, { message: 'Enter a number of recipients.' });
    min(path.confirmRecipientsAbove, RECIPIENTS_MIN, {
      message: `Use at least ${RECIPIENTS_MIN}.`,
    });
    max(path.confirmRecipientsAbove, RECIPIENTS_MAX, { message: `Use at most ${RECIPIENTS_MAX}.` });
    disabled(path, () => !this.store.canEdit());
  });

  protected readonly dirty = computed(
    () => this.store.team() !== null && !sameModel(this.model(), this._saved()),
  );

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }

  protected setColor(color: TeamColor): void {
    if (this.store.canEdit()) {
      this.model.update((model) => ({ ...model, avatarColor: color }));
    }
  }

  protected discard(): void {
    this.model.set(this._saved());
    this.form().reset();
  }

  /** Loads the version someone else saved; the form then shows it and the edits made here are gone. */
  protected reloadAfterConflict(): void {
    this.conflict.set(false);
    this.store.reload();
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();

    await submit(this.form, async () => {
      const model = this.model();
      const outcome = await this.store.save({
        name: model.name.trim(),
        description: model.description.trim() || null,
        serviceAccountEmail: model.serviceAccountEmail.trim(),
        avatarColor: model.avatarColor,
        defaultSignature: model.defaultSignature || null,
        confirmRecipientsAbove: model.confirmRecipientsAbove,
      });

      if (outcome === 'saved') {
        this.form().reset();
      }

      this.conflict.set(outcome === 'conflict');

      return undefined;
    });
  }

  protected askLeave(): void {
    this.confirmingLeave.set(true);
  }

  protected cancelLeave(): void {
    if (!this.store.leaving()) {
      this.confirmingLeave.set(false);
    }
  }

  protected async leave(): Promise<void> {
    const left = await this.store.leave();

    this.confirmingLeave.set(false);

    if (left) {
      // Unsaved edits belong to a team the person can no longer reach.
      this.model.set(this._saved());
      await this._router.navigateByUrl(`/${routePaths.home}`);
    }
  }
}
