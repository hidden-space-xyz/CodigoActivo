export const partnerQueryKeys = {
  all: ['partners'] as const,
  sponsors: () => [...partnerQueryKeys.all, 'sponsors'] as const,
  adminTable: () => [...partnerQueryKeys.all, 'admin'] as const,
}
