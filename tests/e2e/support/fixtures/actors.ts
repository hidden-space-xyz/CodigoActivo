import { mkdir, readFile, writeFile } from 'node:fs/promises'
import path from 'node:path'

import {
  changeUserType,
  grantStaffRole,
  registerAccount,
  signIn,
  type Account,
  type Registration,
} from '../api/accounts'
import { ApiClient, type StorageState } from '../api/api-client'
import type { UserTypeName } from '../api/catalogs'
import { currentUser } from '../api/my-account'
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

/** How to create an account: what to register, its type and its staff role, which needs a member. */
export interface ActorOptions extends Registration {
  readonly type?: UserTypeName
  readonly admin?: boolean
  readonly editor?: boolean
}

const TESTS_PER_ADMINISTRATOR = 5

interface SavedActor {
  readonly account: Account
  readonly state: StorageState
}

function stateFile(name: SharedActorName): string {
  return path.join(STATE_DIRECTORY, `${name}.json`)
}

/** The account the API knows for the session of `api`. */
export async function accountOf(api: ApiClient, password: string): Promise<Account> {
  const user = await currentUser(api)
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
 * The administrators of a worker. The API limits the requests of each account per minute and the
 * tests of an administrator make many, so a new account takes over every few tests.
 */
export class AdminRoster {
  private current: Actor | undefined
  private tests = 0

  constructor(private readonly create: () => Promise<Actor>) {}

  /** The administrator of the next test. */
  async next(): Promise<Actor> {
    if (!this.current || this.tests === TESTS_PER_ADMINISTRATOR) {
      await this.dispose()
      this.current = await this.create()
      this.tests = 0
    }
    this.tests += 1
    return this.current
  }

  /** Closes the session of the current administrator. */
  async dispose(): Promise<void> {
    await this.current?.api.dispose()
  }
}

/**
 * Registers and verifies a new account, makes it an administrator or an editor or changes its type
 * through `initialAdmin` when asked, and returns it without signing it in.
 */
export async function createAccount(
  options: ActorOptions,
  anonymous: ApiClient,
  mailbox: Mailbox,
  initialAdmin: Actor,
): Promise<Account> {
  const account = await registerAccount(anonymous, mailbox, options, initialAdmin.api)
  if (options.type && options.type !== 'participant') {
    await changeUserType(initialAdmin.api, account.id, options.type)
  }
  if (options.admin) await grantStaffRole(initialAdmin.api, account.id, 'Administrator')
  else if (options.editor) await grantStaffRole(initialAdmin.api, account.id, 'Editor')
  return account
}
