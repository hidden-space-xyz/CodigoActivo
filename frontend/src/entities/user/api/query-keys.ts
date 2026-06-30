export const userQueryKeys = {
  all: ['users'] as const,
  detail: (id: string) => ['users', id] as const,
}
