import { expect, test } from '@playwright/test'

import { ApiClient } from '../../support/api/api-client'
import { environment } from '../../support/config/environment'
import { saveActor } from '../../support/fixtures/actors'
import { takeOver, takeOverAdministrator } from '../../support/fixtures/shared-actors'
import { Mailbox } from '../../support/mail/mailbox'

const SETUP_ADDRESS = '203.0.113.250'

test.describe('stack', () => {
  test.describe.configure({ mode: 'serial' })

  let mailbox: Mailbox
  let initialAdmin: ApiClient

  test.beforeAll(async () => {
    mailbox = await Mailbox.open()
    initialAdmin = await ApiClient.open({ clientAddress: SETUP_ADDRESS })
  })

  test.afterAll(async () => {
    await initialAdmin.dispose()
    await mailbox.dispose()
  })

  test('the initial administrator takes over its account', async () => {
    const account = await takeOver(initialAdmin, mailbox, environment.initialAdminEmail)
    await saveActor('initial-admin', account, initialAdmin)
    expect(account.email).toBe(environment.initialAdminEmail)
  })

  test('the privacy officer is an administrator', async () => {
    const officer = await ApiClient.open({ clientAddress: SETUP_ADDRESS })
    try {
      const email = environment.privacyOfficerEmail
      const account = await takeOverAdministrator(initialAdmin, officer, mailbox, email)
      await saveActor('privacy-officer', account, officer)
      expect(account.email).toBe(email)
    } finally {
      await officer.dispose()
    }
  })
})
