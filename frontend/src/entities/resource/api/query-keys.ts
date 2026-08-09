export const resourceQueryKeys = {
  all: ['resources'] as const,
  list: () => [...resourceQueryKeys.all, 'list'] as const,
  detail: (id: string) => [...resourceQueryKeys.all, 'detail', id] as const,
  adminTable: () => [...resourceQueryKeys.all, 'admin'] as const,
}
