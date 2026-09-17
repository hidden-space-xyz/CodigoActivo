/** Query keys for session state that is fetched rather than held locally. */
export const sessionQueryKeys = {
  all: ['session'] as const,
  loginChallenge: () => [...sessionQueryKeys.all, 'login-challenge'] as const,
}
