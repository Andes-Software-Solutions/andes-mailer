import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { TeamMember, TeamRole } from '@core/api/team-member.model';
import { SessionStore } from '@core/auth/session-store';
import { ConfirmDialog } from '@shared/dialog/confirm-dialog';
import { EmptyState } from '@shared/empty-state/empty-state';
import { initials } from '@shared/formatting/initials';
import { PHONE_QUERY, TABLET_DOWN_QUERY, mediaQuery } from '@shared/layout/media-query';
import { AddFailure, ChangeOutcome, RoleFilter, TeamMembersStore } from './team-members-store';
import { AddMembers, AddMembersRequest } from './add-members';
import { MemberActions } from './member-actions';

/** A question waiting for the person's answer before a change is sent. */
type Pending =
  | { readonly kind: 'promote'; readonly member: TeamMember }
  | { readonly kind: 'remove'; readonly member: TeamMember }
  /** `name` is `null` when the only administrator is the person themselves. */
  | { readonly kind: 'last-administrator'; readonly name: string | null };

interface RoleFilterOption {
  readonly value: RoleFilter;
  readonly label: string;
}

/** The active team's members: who is in it and the role each one holds. */
@Component({
  selector: 'app-team-members',
  imports: [AddMembers, ConfirmDialog, DatePipe, EmptyState, MemberActions],
  providers: [TeamMembersStore],
  styleUrl: './team-members.scss',
  templateUrl: './team-members.html',
  host: { class: 'd-flex flex-column gap-4' },
})
export class TeamMembers {
  protected readonly store = inject(TeamMembersStore);
  private readonly _session = inject(SessionStore);

  protected readonly phone = mediaQuery(PHONE_QUERY);
  protected readonly tablet = mediaQuery(TABLET_DOWN_QUERY);

  protected readonly team = this._session.activeTeam;
  protected readonly canAdd = computed(() => this._session.can('CreateTeamUser'));
  protected readonly canChange = computed(() => this._session.can('ModifyTeamUser'));
  protected readonly canRemove = computed(() => this._session.can('DeactivateTeamUser'));
  protected readonly selfId = computed(() => this._session.user()?.id ?? null);

  protected readonly filters: readonly RoleFilterOption[] = [
    { value: 'all', label: 'All' },
    { value: 'administrators', label: 'Administrators' },
    { value: 'members', label: 'Members' },
  ];

  protected readonly adding = signal(false);
  protected readonly addFailures = signal<readonly AddFailure[]>([]);
  protected readonly pending = signal<Pending | null>(null);
  protected readonly busy = signal(false);
  protected readonly expanded = signal<string | null>(null);

  protected readonly filtered = computed(
    () => this.store.search().trim().length > 0 || this.store.roleFilter() !== 'all',
  );

  protected initials(name: string): string {
    return initials(name);
  }

  protected isNew(member: TeamMember): boolean {
    return this.store.newIds().includes(member.userId);
  }

  protected isPending(member: TeamMember): boolean {
    return this.store.pendingIds().includes(member.userId);
  }

  protected onSearch(event: Event): void {
    this.store.setSearch((event.target as HTMLInputElement).value);
  }

  protected toggleExpanded(member: TeamMember): void {
    this.expanded.update((current) => (current === member.userId ? null : member.userId));
  }

  protected openAdd(): void {
    this.addFailures.set([]);
    this.adding.set(true);
  }

  protected closeAdd(): void {
    if (!this.store.adding()) {
      this.adding.set(false);
    }
  }

  protected async add(request: AddMembersRequest): Promise<void> {
    const failures = await this.store.addMany(request.people, request.role);

    this.addFailures.set(failures);

    if (failures.length === 0) {
      this.adding.set(false);
    }
  }

  /** The select shows the saved role until the change is saved, so a cancelled one leaves no trace. */
  protected onRoleChange(member: TeamMember, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const role = select.value as TeamRole;

    select.value = member.role;

    if (role === member.role) {
      return;
    }

    if (role === 'TeamAdministrator') {
      this.pending.set({ kind: 'promote', member });
    } else {
      void this.apply(member, this.store.changeRole(member, role));
    }
  }

  protected askRemove(member: TeamMember): void {
    this.pending.set(
      this.store.isLastAdministrator(member)
        ? this.lastAdministrator(member)
        : { kind: 'remove', member },
    );
  }

  protected async confirm(): Promise<void> {
    const pending = this.pending();

    if (!pending || pending.kind === 'last-administrator') {
      this.pending.set(null);

      return;
    }

    this.busy.set(true);

    try {
      const outcome =
        pending.kind === 'promote'
          ? await this.store.changeRole(pending.member, 'TeamAdministrator')
          : await this.store.remove(pending.member);

      this.pending.set(
        outcome === 'last-administrator' ? this.lastAdministrator(pending.member) : null,
      );
    } finally {
      this.busy.set(false);
    }
  }

  protected cancel(): void {
    if (!this.busy()) {
      this.pending.set(null);
    }
  }

  protected roleLabel(role: TeamRole): string {
    return role === 'TeamAdministrator' ? 'Administrator' : 'Member';
  }

  private async apply(member: TeamMember, outcome: Promise<ChangeOutcome>): Promise<void> {
    if ((await outcome) === 'last-administrator') {
      this.pending.set(this.lastAdministrator(member));
    }
  }

  private lastAdministrator(member: TeamMember): Pending {
    return {
      kind: 'last-administrator',
      name: member.userId === this.selfId() ? null : member.fullName,
    };
  }
}
