import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { idempotent } from './idempotency/idempotency-key';
import { Paginated } from './paginated.model';
import { CreateTeamRequest, Team, TeamSummary, UpdateTeamRequest } from './team.model';

const TEAMS_URL = '/api/teams';

/** The API's largest page; every team of a small organisation fits in one. */
const PAGE_SIZE = 100;

/** `/api/teams`. Every write takes the `Idempotency-Key` of the user intent it belongs to. */
@Service()
export class TeamsApi {
  private readonly _http = inject(HttpClient);

  list(): Observable<Paginated<TeamSummary>> {
    return this._http.get<Paginated<TeamSummary>>(TEAMS_URL, { params: { pageSize: PAGE_SIZE } });
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
