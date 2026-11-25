import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { EmailSuggestion } from '@shared/email-autocomplete/email-autocomplete';
import { Observable, map } from 'rxjs';
import { DirectoryUser } from './directory-user.model';

/** The API rejects shorter queries. */
export const DIRECTORY_QUERY_MIN_LENGTH = 2;

/** `GET /api/users/directory`: people in Entra, rate limited per caller. */
@Service()
export class DirectoryApi {
  private readonly _http = inject(HttpClient);

  search(query: string, limit = 10): Observable<readonly DirectoryUser[]> {
    return this._http.get<readonly DirectoryUser[]>('/api/users/directory', {
      params: { query, limit },
    });
  }

  /** Directory accounts as email suggestions; shared mailboxes are accounts too, so they show up. */
  suggestEmails(query: string): Observable<readonly EmailSuggestion[]> {
    return this.search(query).pipe(
      map((people) => people.map((person) => ({ email: person.email, label: person.fullName }))),
    );
  }
}
