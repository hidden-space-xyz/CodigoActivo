import type { TranslationKey } from '@/shared/i18n'

/** Staff role of an account in the admin area; an administrator can do everything an editor can. */
export type StaffRole = 'None' | 'Editor' | 'Administrator'

/** Every staff role, in the order selectors list them. */
export const STAFF_ROLES: readonly StaffRole[] = ['None', 'Editor', 'Administrator']

const STAFF_ROLE_LABEL_KEYS: Record<StaffRole, TranslationKey> = {
  None: 'entities.user.staffRole.None',
  Editor: 'entities.user.staffRole.Editor',
  Administrator: 'entities.user.staffRole.Administrator',
}

/** Translation key of the label shown for a staff role. */
export function staffRoleLabelKey(staffRole: StaffRole): TranslationKey {
  return STAFF_ROLE_LABEL_KEYS[staffRole]
}
