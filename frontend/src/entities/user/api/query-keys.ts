/** Query keys for the admin users table; invalidate `all` after any user mutation. */
export const userQueryKeys = {
  all: ['users'] as const,
  adminTable: () => [...userQueryKeys.all, 'table'] as const,
}
