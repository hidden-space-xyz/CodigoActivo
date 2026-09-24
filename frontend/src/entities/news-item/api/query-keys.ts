/** Query keys for public news data; `byYear` also varies by the search text. */
export const newsQueryKeys = {
  all: ['news'] as const,
  publicDetail: (id: string) => [...newsQueryKeys.all, 'public', id] as const,
  years: () => [...newsQueryKeys.all, 'years'] as const,
  byYear: (year: string, search: string) => [...newsQueryKeys.all, 'year', year, search] as const,
  home: () => [...newsQueryKeys.all, 'home'] as const,
}
