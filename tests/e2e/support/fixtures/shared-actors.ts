import {
  changeUserType,
  designatePrivacyOfficer,
  findUser,
  grantStaffRole,
  registerAccount,
  resetPassword,
  signIn,
  type Account,
} from '../api/accounts'
import { ApiRequestError, type ApiClient } from '../api/api-client'
import { PASSWORD } from '../config/environment'
import type { Mailbox } from '../mail/mailbox'
import { accountOf } from './actors'

/**
 * Signs `email` in with the suite's password, first setting it through a password reset when the
 * account does not have it yet, as the initial administrator after a fresh start.
 */
export async function takeOver(api: ApiClient, mailbox: Mailbox, email: string): Promise<Account> {
  const credentials = { email, password: PASSWORD }
  try {
    await signIn(api, mailbox, credentials)
  } catch (error) {
    if (!(error instanceof ApiRequestError) || error.code !== 'InvalidCredentials') throw error
    await resetPassword(api, mailbox, credentials)
    await signIn(api, mailbox, credentials)
  }
  return accountOf(api, PASSWORD)
}

/**
 * Has the initial administrator make `email` a member and an administrator and designate it for
 * data protection, registering it first when nobody has it, and signs it in on `api`.
 */
export async function takeOverPrivacyOfficer(
  initialAdmin: ApiClient,
  api: ApiClient,
  mailbox: Mailbox,
  email: string,
): Promise<Account> {
  const existing = await findUser(initialAdmin, email)
  const id = existing?.id ?? (await registerAccount(api, mailbox, { label: 'officer', email })).id
  if (!existing?.isPrivacyOfficer) {
    await changeUserType(initialAdmin, id, 'member')
    await grantStaffRole(initialAdmin, id, 'Administrator')
    await designatePrivacyOfficer(initialAdmin, id)
  }
  return takeOver(api, mailbox, email)
}
