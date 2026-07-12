export const resourceQueryKeys = {
  all: ['resources'] as const,
  list: () => ['resources', 'list'] as const,
  detail: (id: string) => ['resources', id] as const,
}
