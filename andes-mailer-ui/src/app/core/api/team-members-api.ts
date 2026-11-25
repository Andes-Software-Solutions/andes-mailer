import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { allPages } from './all-pages';
import { idempotent } from './idempotency/idempotency-key';
import { Paginated } from './paginated.model';
import { AddTeamMemberRequest, TeamMember, UpdateTeamMemberRequest } from './team-member.model';

/** The API's largest page. */
const PAGE_SIZE = 100;

function membersUrl(teamId: string): string {
  return `/api/teams/${teamId}/members`;
}

/** `/api/teams/{teamId}/members`. Every write takes the `Idempotency-Key` of its user intent. */
@Service()
export class TeamMembersApi {
  private readonly _http = inject(HttpClient);

  /** Every member, across as many pages as the team needs. */
  list(teamId: string): Observable<Paginated<TeamMember>> {
    return allPages((page) =>
      this._http.get<Paginated<TeamMember>>(membersUrl(teamId), {
        params: { page, pageSize: PAGE_SIZE },
      }),
    );
  }

  add(
    teamId: string,
    request: AddTeamMemberRequest,
    idempotencyKey: string,
  ): Observable<TeamMember> {
    return this._http.post<TeamMember>(membersUrl(teamId), request, {
      context: idempotent(idempotencyKey),
    });
  }

  update(
    teamId: string,
    userId: string,
    request: UpdateTeamMemberRequest,
    idempotencyKey: string,
  ): Observable<TeamMember> {
    return this._http.put<TeamMember>(`${membersUrl(teamId)}/${userId}`, request, {
      context: idempotent(idempotencyKey),
    });
  }

  remove(teamId: string, userId: string, idempotencyKey: string): Observable<void> {
    return this._http.delete<void>(`${membersUrl(teamId)}/${userId}`, {
      context: idempotent(idempotencyKey),
    });
  }

  /** Removes the signed-in user from the team. */
  leave(teamId: string, idempotencyKey: string): Observable<void> {
    return this._http.delete<void>(`${membersUrl(teamId)}/me`, {
      context: idempotent(idempotencyKey),
    });
  }
}
