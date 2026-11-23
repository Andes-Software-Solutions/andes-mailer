import { Component, computed, inject } from '@angular/core';
import { SessionStore } from '@core/auth/session-store';
import { EmptyState } from '@shared/empty-state/empty-state';

@Component({
  imports: [EmptyState],
  selector: 'app-home',
  styleUrl: './home.scss',
  templateUrl: './home.html',
  host: { class: 'd-flex flex-column gap-4' },
})
export class Home {
  protected readonly session = inject(SessionStore);

  protected readonly firstName = computed(() => this.session.user()?.name.split(' ')[0] ?? '');
}
