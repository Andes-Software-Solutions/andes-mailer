import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Team, TeamSummary } from '@core/api/team.model';
import { SessionStore } from '@core/auth/session-store';
import { ToastStore } from '@core/feedback/toast-store';
import { routePaths } from '@core/navigation/route-paths';
import { ConfirmDialog } from '@shared/dialog/confirm-dialog';
import { EmptyState } from '@shared/empty-state/empty-state';
import { TeamAvatar } from '@shared/team-avatar/team-avatar';
import { NewTeam } from './new-team';
import { TeamsStore } from './teams-store';

/** Every team in the app, where a global administrator creates and deactivates them. */
@Component({
  selector: 'app-teams',
  imports: [ConfirmDialog, EmptyState, NewTeam, TeamAvatar],
  providers: [TeamsStore],
  styleUrl: './teams.scss',
  templateUrl: './teams.html',
  host: { class: 'd-flex flex-column gap-4' },
})
export class Teams {
  protected readonly store = inject(TeamsStore);
  private readonly _session = inject(SessionStore);
  private readonly _router = inject(Router);
  private readonly _toasts = inject(ToastStore);

  protected readonly creating = signal(false);
  protected readonly deactivating = signal<TeamSummary | null>(null);

  protected readonly sorted = computed(() =>
    [...this.store.entities()].sort((a, b) => a.name.localeCompare(b.name)),
  );

  protected readonly summary = computed(() => {
    const count = this.store.entities().length;

    return `${count} team${count === 1 ? '' : 's'}`;
  });

  protected roleLabel(team: TeamSummary): string {
    switch (team.role) {
      case 'TeamAdministrator':
        return 'Administrator';
      case 'TeamMember':
        return 'Member';
      default:
        return 'Not a member';
    }
  }

  protected async open(team: TeamSummary): Promise<void> {
    if (await this.switchTo(team.id, team.name)) {
      await this._router.navigateByUrl(`/${routePaths.teamSettings}`);
    }
  }

  protected async created(team: Team): Promise<void> {
    this.creating.set(false);

    // A new team has no members; its first stop is adding them.
    if (await this.switchTo(team.id, team.name)) {
      await this._router.navigateByUrl(`/${routePaths.teamMembers}`);
    }
  }

  protected async confirmDeactivate(): Promise<void> {
    const team = this.deactivating();

    if (team && (await this.store.deactivate(team))) {
      this.deactivating.set(null);
    }
  }

  protected cancelDeactivate(): void {
    if (!this.store.deactivatingId()) {
      this.deactivating.set(null);
    }
  }

  /**
   * Makes the team active before a team page opens, so the page never shows another team. A team
   * made since the account loaded needs the account read again first.
   */
  private async switchTo(teamId: string, name: string): Promise<boolean> {
    if (this._session.selectTeam(teamId)) {
      return true;
    }

    await this._session.refresh();

    if (this._session.selectTeam(teamId)) {
      return true;
    }

    this._toasts.show({
      tone: 'error',
      message: `We couldn't open ${name}. Reload the page and try again.`,
    });

    return false;
  }
}
