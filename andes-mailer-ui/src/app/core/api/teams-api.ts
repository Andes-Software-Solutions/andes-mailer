import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { allPages } from './all-pages';
import { idempotent } from './idempotency/idempotency-key';
import { Paginated } from './paginated.model';
import { CreateTeamRequest, Team, TeamSummary, UpdateTeamRequest } from './team.model';

const TEAMS_URL = '/api/teams';

/** The API's largest page. */
const PAGE_SIZE = 100;

/** `/api/teams`. Every write takes the `Idempotency-Key` of the user intent it belongs to. */
@Service()
export class TeamsApi {
  private readonly _http = inject(HttpClient);

  /** Every team the caller sees, across as many pages as there are. */
  list(): Observable<Paginated<TeamSummary>> {
    return allPages((page) =>
      this._http.get<Paginated<TeamSummary>>(TEAMS_URL, { params: { page, pageSize: PAGE_SIZE } }),
    );
  }

  get(teamId: string): Observable<Team> {
    return this._http.get<Team>(`${TEAMS_URL}/${teamId}`);
  }

  create(request: CreateTeamRequest, idempotencyKey: string): Observable<Team> {
    return this._http.post<Team>(TEAMS_URL, request, { context: idempotent(idempotencyKey) });
  }

  update(teamId: string, request: UpdateTeamRequest, idempotencyKey: string): Observable<Team> {
    return this._http.put<Team>(`${TEAMS_URL}/${teamId}`, request, {
      context: idempotent(idempotencyKey),
    });
  }

  deactivate(teamId: string, idempotencyKey: string): Observable<void> {
    return this._http.delete<void>(`${TEAMS_URL}/${teamId}`, {
      context: idempotent(idempotencyKey),
    });
  }
}
