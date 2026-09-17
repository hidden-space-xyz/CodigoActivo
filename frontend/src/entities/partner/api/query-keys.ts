/** Query keys for partners; public sponsors and the admin table share the `all` root. */
export const partnerQueryKeys = {
  all: ['partners'] as const,
  sponsors: () => [...partnerQueryKeys.all, 'sponsors'] as const,
  adminTable: () => [...partnerQueryKeys.all, 'admin'] as const,
}
