export const resourceQueryKeys = {
  all: ['resources'] as const,
  list: (search: string) => [...resourceQueryKeys.all, 'list', search] as const,
  detail: (id: string) => [...resourceQueryKeys.all, 'detail', id] as const,
  adminTable: () => [...resourceQueryKeys.all, 'admin'] as const,
}
