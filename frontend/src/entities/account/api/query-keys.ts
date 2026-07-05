export const accountQueryKeys = {
  all: ['account'] as const,
  me: () => [...accountQueryKeys.all, 'me'] as const,
  children: () => [...accountQueryKeys.all, 'children'] as const,
  // Registration-type lookups are shared by the register flow and the account page.
  registrationTypes: (minorsOnly: boolean) =>
    ['registration-types', minorsOnly ? 'minor' : 'adult'] as const,
}
