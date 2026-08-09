export const userQueryKeys = {
  all: ['users'] as const,
  adminTable: () => [...userQueryKeys.all, 'table'] as const,
}
