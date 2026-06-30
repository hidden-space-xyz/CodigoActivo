export const resourceQueryKeys = {
  all: ['resources'] as const,
  detail: (id: string) => ['resources', id] as const,
}
