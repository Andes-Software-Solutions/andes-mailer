import { firstValueFrom, of } from 'rxjs';
import { vi } from 'vitest';
import { allPages } from './all-pages';
import { Paginated } from './paginated.model';

function page(number: number, items: readonly string[], totalCount: number): Paginated<string> {
  return { items, page: number, pageSize: 2, totalCount };
}

describe('allPages', () => {
  it('loads every page and joins their items', async () => {
    const pages = [page(1, ['a', 'b'], 5), page(2, ['c', 'd'], 5), page(3, ['e'], 5)];
    const fetchPage = vi.fn((number: number) => of(pages[number - 1]));

    const all = await firstValueFrom(allPages(fetchPage));

    expect(all.items).toEqual(['a', 'b', 'c', 'd', 'e']);
    expect(all.totalCount).toBe(5);
    expect(fetchPage.mock.calls.map(([number]) => number)).toEqual([1, 2, 3]);
  });

  it('asks once when the first page holds everything', async () => {
    const fetchPage = vi.fn(() => of(page(1, ['a'], 1)));

    const all = await firstValueFrom(allPages(fetchPage));

    expect(all.items).toEqual(['a']);
    expect(fetchPage).toHaveBeenCalledTimes(1);
  });

  it('stops at an empty page even if the count says there is more', async () => {
    const fetchPage = vi.fn((number: number) =>
      of(number === 1 ? page(1, ['a', 'b'], 9) : page(number, [], 9)),
    );

    const all = await firstValueFrom(allPages(fetchPage));

    expect(all.items).toEqual(['a', 'b']);
    expect(fetchPage).toHaveBeenCalledTimes(2);
  });
});
