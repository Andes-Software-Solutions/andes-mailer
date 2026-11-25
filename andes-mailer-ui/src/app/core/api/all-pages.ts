import { EMPTY, Observable, expand, reduce } from 'rxjs';
import { Paginated } from './paginated.model';

/**
 * Fetches page after page until `totalCount` is reached, and emits them as one page holding every
 * item. The API caps the page size, not the list, so a caller that treats the result as the whole
 * list must load the rest.
 */
export function allPages<T>(
  fetchPage: (page: number) => Observable<Paginated<T>>,
): Observable<Paginated<T>> {
  return fetchPage(1).pipe(
    expand((last) =>
      last.items.length > 0 && last.page * last.pageSize < last.totalCount
        ? fetchPage(last.page + 1)
        : EMPTY,
    ),
    reduce((all, next) => ({ ...next, page: 1, items: [...all.items, ...next.items] })),
  );
}
