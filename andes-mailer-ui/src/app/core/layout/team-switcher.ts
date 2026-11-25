import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  booleanAttribute,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { CurrentUserTeam } from '@core/api/current-user.model';
import { SessionStore } from '@core/auth/session-store';
import { TeamAvatar } from '@shared/team-avatar/team-avatar';
import { attachDropdown } from './attach-dropdown';
import { RoleBadge, roleBadge } from './role-badge';

interface TeamOption {
  readonly team: CurrentUserTeam;
  readonly badge: RoleBadge | null;
}

/** Picks the team the person works in; the nav and every permission check follow it. */
@Component({
  selector: 'app-team-switcher',
  imports: [TeamAvatar],
  styleUrl: './team-switcher.scss',
  templateUrl: './team-switcher.html',
  host: { '[class.compact]': 'compact()' },
})
export class TeamSwitcher {
  private readonly _session = inject(SessionStore);
  private readonly _toggle = viewChild.required<ElementRef<HTMLButtonElement>>('toggle');

  /** The small top-bar chip used while the sidebar is a rail. */
  readonly compact = input(false, { transform: booleanAttribute });

  protected readonly options = computed<readonly TeamOption[]>(() =>
    this._session.teams().map((team) => ({
      team,
      badge: roleBadge(team.role, this._session.isAdministrator()),
    })),
  );

  protected readonly active = computed(() => {
    const id = this._session.activeTeam()?.id;

    return this.options().find((option) => option.team.id === id) ?? null;
  });

  protected readonly toggleLabel = computed(() => {
    const current = this.active();

    return current ? `Team: ${current.team.name}. Switch team` : 'No team yet';
  });

  constructor() {
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => attachDropdown(this._toggle().nativeElement, destroyRef));
  }

  protected select(teamId: string): void {
    this._session.selectTeam(teamId);
  }
}
