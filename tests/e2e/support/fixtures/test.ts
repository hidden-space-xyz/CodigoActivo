import { test as base, type BrowserContext, type Page } from '@playwright/test'
import type { EventCategoryTypeResponse } from '@contract/eventCategoryTypeResponse'

import type { Account } from '../api/accounts'
import { ApiClient } from '../api/api-client'
import { ContentSeeder, createCategory } from '../api/content'
import { environment } from '../config/environment'
import { Mailbox } from '../mail/mailbox'
import { actorFor, createAccount, loadActor, type Actor, type ActorOptions } from './actors'

/** Creates accounts a single test owns, for flows that change or remove them. */
interface Accounts {
  /** Registers and verifies a new account, without signing it in. */
  create(options: ActorOptions): Promise<Account>
  /** Signs `account` in through the API. */
  signIn(account: Account): Promise<Actor>
  /** Registers, verifies and signs in a new account. */
  actor(options: ActorOptions): Promise<Actor>
}

interface WorkerFixtures {
  clientAddress: string
  mailbox: Mailbox
  anonymousApi: ApiClient
  initialAdmin: Actor
  privacyOfficer: Actor
  admin: Actor
  editor: Actor
  category: EventCategoryTypeResponse
  member: Actor
  participant: Actor
  guardian: Actor
}

interface TestFixtures {
  accounts: Accounts
  seed: ContentSeeder
  pageAs: (actor: Actor) => Promise<Page>
}

const WORKER_SETUP = { scope: 'worker', timeout: 120_000 } as const

/**
 * The suite's `test`: a visitor `page`, worker-wide actors (the initial administrator, the
 * privacy officer, an administrator, a member, a participant and a guardian with two dependents),
 * `pageAs` to open the site signed in as any of them, `accounts` for accounts a test owns and
 * `seed` to create content through the API. Each worker reports its own client address, so the
 * rate limits treat workers as different visitors.
 */
export const test = base.extend<TestFixtures, WorkerFixtures>({
  clientAddress: [
    async ({}, use, workerInfo) => {
      await use(`203.0.113.${environment.clientAddressBase + workerInfo.parallelIndex}`)
    },
    { scope: 'worker' },
  ],
  mailbox: [
    async ({}, use) => {
      const mailbox = await Mailbox.open()
      await use(mailbox)
      await mailbox.dispose()
    },
    { scope: 'worker' },
  ],
  anonymousApi: [
    async ({ clientAddress }, use) => {
      const api = await ApiClient.open({ clientAddress })
      await use(api)
      await api.dispose()
    },
    { scope: 'worker' },
  ],
  initialAdmin: [
    async ({ clientAddress }, use) => {
      const actor = await loadActor('initial-admin', clientAddress)
      await use(actor)
      await actor.api.dispose()
    },
    { scope: 'worker' },
  ],
  privacyOfficer: [
    async ({ clientAddress }, use) => {
      const actor = await loadActor('privacy-officer', clientAddress)
      await use(actor)
      await actor.api.dispose()
    },
    { scope: 'worker' },
  ],
  admin: [
    async ({ anonymousApi, mailbox, initialAdmin, clientAddress }, use) => {
      const account = await createAccount(
        { label: 'admin', type: 'member', admin: true },
        anonymousApi,
        mailbox,
        initialAdmin,
      )
      const actor = await actorFor(account, mailbox, clientAddress)
      await use(actor)
      await actor.api.dispose()
    },
    WORKER_SETUP,
  ],
  editor: [
    async ({ anonymousApi, mailbox, initialAdmin, clientAddress }, use) => {
      const account = await createAccount(
        { label: 'editor', type: 'member', editor: true },
        anonymousApi,
        mailbox,
        initialAdmin,
      )
      const actor = await actorFor(account, mailbox, clientAddress)
      await use(actor)
      await actor.api.dispose()
    },
    WORKER_SETUP,
  ],
  category: [
    async ({ admin }, use) => {
      await use(await createCategory(admin.api))
    },
    { scope: 'worker' },
  ],
  member: [
    async ({ anonymousApi, mailbox, initialAdmin, clientAddress }, use) => {
      const account = await createAccount(
        { label: 'member', type: 'member', newsConsent: true },
        anonymousApi,
        mailbox,
        initialAdmin,
      )
      const actor = await actorFor(account, mailbox, clientAddress)
      await use(actor)
      await actor.api.dispose()
    },
    WORKER_SETUP,
  ],
  participant: [
    async ({ anonymousApi, mailbox, initialAdmin, clientAddress }, use) => {
      const account = await createAccount(
        { label: 'participant' },
        anonymousApi,
        mailbox,
        initialAdmin,
      )
      const actor = await actorFor(account, mailbox, clientAddress)
      await use(actor)
      await actor.api.dispose()
    },
    WORKER_SETUP,
  ],
  guardian: [
    async ({ anonymousApi, mailbox, initialAdmin, clientAddress }, use) => {
      const account = await createAccount(
        { label: 'guardian', minors: 2 },
        anonymousApi,
        mailbox,
        initialAdmin,
      )
      const actor = await actorFor(account, mailbox, clientAddress)
      await use(actor)
      await actor.api.dispose()
    },
    WORKER_SETUP,
  ],
  extraHTTPHeaders: async ({ clientAddress }, use) => {
    await use({ 'X-Forwarded-For': clientAddress })
  },
  accounts: async ({ anonymousApi, mailbox, initialAdmin, clientAddress }, use) => {
    const opened: ApiClient[] = []
    const signIn = async (account: Account): Promise<Actor> => {
      const actor = await actorFor(account, mailbox, clientAddress)
      opened.push(actor.api)
      return actor
    }
    const create = (options: ActorOptions) =>
      createAccount(options, anonymousApi, mailbox, initialAdmin)
    await use({ create, signIn, actor: async (options) => signIn(await create(options)) })
    await Promise.all(opened.map((api) => api.dispose()))
  },
  seed: async ({ admin, category }, use) => {
    await use(new ContentSeeder(admin.api, category))
  },
  pageAs: async ({ browser }, use) => {
    const contexts: BrowserContext[] = []
    await use(async (actor) => {
      const context = await browser.newContext({ storageState: actor.state })
      contexts.push(context)
      return context.newPage()
    })
    await Promise.all(contexts.map((context) => context.close()))
  },
})

export { expect } from '@playwright/test'
