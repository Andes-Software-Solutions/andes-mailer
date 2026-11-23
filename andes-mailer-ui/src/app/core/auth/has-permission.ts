import {
  Directive,
  TemplateRef,
  ViewContainerRef,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import { Permission } from '@core/api/permission.model';
import { PermissionMode, SessionStore } from './session-store';

/**
 * Renders its template only for someone who holds the permissions in the active team or
 * globally, and follows team switches.
 *
 * ```html
 * <a *appHasPermission="'SendTeamEmails'">…</a>
 * <div *appHasPermission="['ModifyTeamUser', 'DeactivateTeamUser']; mode: 'all'; else denied">…</div>
 * ```
 *
 * It hides; it does not protect. Routes still need `permissionGuard`, and the API checks every call.
 */
@Directive({ selector: '[appHasPermission]' })
export class HasPermission {
  private readonly _session = inject(SessionStore);
  private readonly _template = inject<TemplateRef<unknown>>(TemplateRef);
  private readonly _viewContainer = inject(ViewContainerRef);

  readonly appHasPermission = input.required<Permission | readonly Permission[]>();
  readonly appHasPermissionMode = input<PermissionMode>('any');
  readonly appHasPermissionElse = input<TemplateRef<unknown> | null>(null);

  /** Recreates the view only when the outcome changes, not on every permission update. */
  private readonly _shown = computed(() =>
    this._session.can(this.appHasPermission(), this.appHasPermissionMode())
      ? this._template
      : this.appHasPermissionElse(),
  );

  constructor() {
    effect(() => {
      const template = this._shown();

      this._viewContainer.clear();

      if (template) {
        this._viewContainer.createEmbeddedView(template);
      }
    });
  }
}
