import { Component, input } from '@angular/core';
import { EmptyState } from '@shared/empty-state/empty-state';

/** Stands in for a page that isn't built yet; each feature replaces its route when it lands. */
@Component({
  selector: 'app-coming-soon',
  imports: [EmptyState],
  template: `
    <div class="d-flex align-items-center gap-3 flex-wrap">
      <h1 class="mb-0">{{ label() }}</h1>
    </div>
    <div class="card">
      <app-empty-state
        icon="tools"
        title="Coming soon"
        text="This part of Andes Mailer is still being built."
      />
    </div>
  `,
  host: { class: 'd-flex flex-column gap-4' },
})
export class ComingSoon {
  /** From the route's `data.label`. */
  readonly label = input('');
}
