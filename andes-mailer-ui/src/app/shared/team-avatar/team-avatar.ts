import { Component, computed, input } from '@angular/core';
import { TeamColor } from '@core/api/team.model';
import { initials } from '@shared/formatting/initials';

/** A team's initials on its avatar colour. Decorative: the team's name is always beside it. */
@Component({
  selector: 'app-team-avatar',
  host: {
    class: 'avatar avatar-team',
    '[class]': "'team-color-' + color().toLowerCase()",
    'aria-hidden': 'true',
  },
  template: `{{ letters() }}`,
})
export class TeamAvatar {
  readonly name = input.required<string>();
  readonly color = input<TeamColor>('Mint');

  protected readonly letters = computed(() => initials(this.name()));
}
