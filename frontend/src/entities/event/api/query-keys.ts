export const eventQueryKeys = {
  all: ['events'] as const,
  upcoming: () => [...eventQueryKeys.all, 'upcoming'] as const,
  board: () => [...eventQueryKeys.all, 'board'] as const,
  past: () => [...eventQueryKeys.all, 'past'] as const,
  detail: (id: string) => [...eventQueryKeys.all, 'detail', id] as const,
}
