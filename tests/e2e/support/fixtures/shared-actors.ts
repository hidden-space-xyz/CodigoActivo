import {
  findUser,
  grantAdmin,
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
 * Makes `email` an administrator, registering it first when nobody has it, and signs it in on
 * `api`. The deployment designates that address as the privacy officer.
 */
export async function takeOverAdministrator(
  initialAdmin: ApiClient,
  api: ApiClient,
  mailbox: Mailbox,
  email: string,
): Promise<Account> {
  const existing = await findUser(initialAdmin, email)
  const id = existing?.id ?? (await registerAccount(api, mailbox, { label: 'admin', email })).id
  if (!existing?.isAdmin) await grantAdmin(initialAdmin, id)
  return takeOver(api, mailbox, email)
}
