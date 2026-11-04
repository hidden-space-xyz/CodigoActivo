import { mkdir, readFile, writeFile } from 'node:fs/promises'
import path from 'node:path'
import type { UserResponse } from '@contract/userResponse'

import {
  changeUserType,
  grantAdmin,
  registerAccount,
  signIn,
  type Account,
  type Registration,
} from '../api/accounts'
import { ApiClient, type StorageState } from '../api/api-client'
import type { UserTypeName } from '../api/catalogs'
import { STATE_DIRECTORY } from '../config/environment'
import type { Mailbox } from '../mail/mailbox'

/** Someone the tests act as: their account and a signed-in API session with its cookies. */
export interface Actor {
  readonly account: Account
  readonly api: ApiClient
  readonly state: StorageState
}

/** A shared account the setup project signed in, saved for the workers. */
export type SharedActorName = 'initial-admin' | 'privacy-officer'

/** How to create an account: what to register, its type and whether it administers the site. */
export interface ActorOptions extends Registration {
  readonly type?: UserTypeName
  readonly admin?: boolean
}

interface SavedActor {
  readonly account: Account
  readonly state: StorageState
}

function stateFile(name: SharedActorName): string {
  return path.join(STATE_DIRECTORY, `${name}.json`)
}

/** The account the API knows for the session of `api`. */
export async function accountOf(api: ApiClient, password: string): Promise<Account> {
  const user = await api.get<UserResponse>('/api/auth/me')
  return {
    id: user.id,
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email ?? '',
    phone: user.phone ?? '',
    nationalId: user.nationalId ?? '',
    gender: user.gender,
    password,
    dependents: [],
  }
}

/** Saves a signed-in shared actor for the workers. */
export async function saveActor(name: SharedActorName, account: Account, api: ApiClient) {
  await mkdir(STATE_DIRECTORY, { recursive: true })
  const saved: SavedActor = { account, state: await api.storageState() }
  await writeFile(stateFile(name), JSON.stringify(saved, null, 2))
}

/** Opens the session the setup project saved for a shared actor. */
export async function loadActor(name: SharedActorName, clientAddress: string): Promise<Actor> {
  const saved = JSON.parse(await readFile(stateFile(name), 'utf8')) as SavedActor
  const api = await ApiClient.open({ clientAddress, storageState: saved.state })
  return { ...saved, api }
}

/** Signs `account` in through the API and returns it as an actor. */
export async function actorFor(
  account: Account,
  mailbox: Mailbox,
  clientAddress: string,
): Promise<Actor> {
  const api = await ApiClient.open({ clientAddress })
  await signIn(api, mailbox, account)
  return { account, api, state: await api.storageState() }
}

/**
 * Registers and verifies a new account, makes it an administrator or changes its type through
 * `initialAdmin` when asked, and returns it without signing it in.
 */
export async function createAccount(
  options: ActorOptions,
  anonymous: ApiClient,
  mailbox: Mailbox,
  initialAdmin: Actor,
): Promise<Account> {
  const account = await registerAccount(anonymous, mailbox, options, initialAdmin.api)
  if (options.admin) await grantAdmin(initialAdmin.api, account.id)
  if (options.type && options.type !== 'participant') {
    await changeUserType(initialAdmin.api, account.id, options.type)
  }
  return account
}
