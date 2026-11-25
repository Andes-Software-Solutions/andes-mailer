import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { TeamMember } from '@core/api/team-member.model';
import { attachDropdown } from '@core/layout/attach-dropdown';

/** The phone card's "More actions" menu for a member. */
@Component({
  selector: 'app-member-actions',
  template: `
    <div class="dropdown">
      <button
        #toggle
        type="button"
        class="btn btn-ghost btn-icon"
        data-bs-toggle="dropdown"
        aria-expanded="false"
        aria-haspopup="menu"
        [attr.aria-label]="'More actions for ' + member().fullName"
        [disabled]="disabled()"
      >
        <i class="bi bi-three-dots-vertical" aria-hidden="true"></i>
      </button>
      <ul class="dropdown-menu dropdown-menu-end" role="menu">
        <li role="none">
          <button
            type="button"
            class="dropdown-item text-danger"
            role="menuitem"
            (click)="remove.emit()"
          >
            <i class="bi bi-person-dash text-danger" aria-hidden="true"></i>
            Remove from team
          </button>
        </li>
      </ul>
    </div>
  `,
})
export class MemberActions {
  private readonly _toggle = viewChild.required<ElementRef<HTMLButtonElement>>('toggle');

  readonly member = input.required<TeamMember>();
  readonly disabled = input(false);
  readonly remove = output<void>();

  constructor() {
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => attachDropdown(this._toggle().nativeElement, destroyRef));
  }
}
