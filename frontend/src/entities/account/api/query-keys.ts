export const accountQueryKeys = {
  all: ['account'] as const,
  me: () => [...accountQueryKeys.all, 'me'] as const,
  children: () => [...accountQueryKeys.all, 'children'] as const,
  history: () => [...accountQueryKeys.all, 'history'] as const,
}
