import { Component, inject, output, signal } from '@angular/core';
import { FormField, email, form, maxLength, required, submit } from '@angular/forms/signals';
import { DirectoryApi } from '@core/api/directory-api';
import { problemMessage, problemStatus } from '@core/api/problem-message';
import { Team, TeamColor, teamColorLabels, teamColors } from '@core/api/team.model';
import { ModalDialog } from '@shared/dialog/modal-dialog';
import { EmailAutocomplete, EmailSuggester } from '@shared/email-autocomplete/email-autocomplete';
import { TeamAvatar } from '@shared/team-avatar/team-avatar';
import { TeamsStore } from './teams-store';

interface NewTeamModel {
  name: string;
  description: string;
  serviceAccountEmail: string;
  avatarColor: TeamColor;
}

/** Mirrors the API's limits (`TeamLimits`). */
const NAME_MAX = 100;
const DESCRIPTION_MAX = 500;
const EMAIL_MAX = 256;

/** Creates a team that sends from a directory mailbox; the API verifies the mailbox exists. */
@Component({
  selector: 'app-new-team',
  imports: [EmailAutocomplete, FormField, ModalDialog, TeamAvatar],
  styleUrl: './new-team.scss',
  templateUrl: './new-team.html',
})
export class NewTeam {
  protected readonly store = inject(TeamsStore);
  private readonly _directory = inject(DirectoryApi);

  protected readonly suggestMailbox: EmailSuggester = (query) =>
    this._directory.suggestEmails(query);

  readonly created = output<Team>();
  readonly dismissed = output<void>();

  protected readonly colors = teamColors;
  protected readonly colorLabels = teamColorLabels;
  protected readonly error = signal<string | null>(null);
  protected readonly mailboxError = signal<string | null>(null);

  protected readonly model = signal<NewTeamModel>({
    name: '',
    description: '',
    serviceAccountEmail: '',
    avatarColor: 'Mint',
  });

  protected readonly form = form(this.model, (path) => {
    required(path.name, { message: 'Enter a team name.' });
    maxLength(path.name, NAME_MAX, { message: `Use ${NAME_MAX} characters or fewer.` });
    maxLength(path.description, DESCRIPTION_MAX, {
      message: `Use ${DESCRIPTION_MAX} characters or fewer.`,
    });
    required(path.serviceAccountEmail, { message: 'Enter the mailbox the team sends from.' });
    email(path.serviceAccountEmail, { message: 'Enter a valid email address.' });
    maxLength(path.serviceAccountEmail, EMAIL_MAX);
  });

  protected setColor(color: TeamColor): void {
    this.model.update((model) => ({ ...model, avatarColor: color }));
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();

    if (this.store.creating()) {
      return;
    }

    await submit(this.form, async () => {
      const model = this.model();

      this.error.set(null);
      this.mailboxError.set(null);

      try {
        const team = await this.store.create({
          name: model.name.trim(),
          description: model.description.trim() || null,
          serviceAccountEmail: model.serviceAccountEmail.trim(),
          avatarColor: model.avatarColor,
        });

        this.created.emit(team);
      } catch (error) {
        const message = problemMessage(error, "We couldn't create the team.");

        // A 404 here always means the directory has no such mailbox.
        if (problemStatus(error) === 404) {
          this.mailboxError.set(message);
        } else {
          this.error.set(message);
        }
      }

      return undefined;
    });
  }
}
