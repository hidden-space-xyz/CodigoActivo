import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import ConsentsSection from '@/pages/account/ui/ConsentsSection.vue'
import type { MyConsentResponse } from '@/shared/api/generated/models'
import { formatDateTime } from '@/shared/lib/date'

import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildConsentResponse } from '../../../../support/builders'
import {
  acceptMessageBox,
  click,
  expectNotification,
  findButton,
  findButtons,
  messageBox,
} from '../../../../support/dom'

function serveConsents(consents: MyConsentResponse[]) {
  let loads = 0
  server.use(
    http.get('/api/me/consents', () => {
      loads += 1
      return HttpResponse.json(consents)
    }),
  )
  return { loads: () => loads }
}

async function renderSection() {
  const rendered = await renderWithProviders(ConsentsSection, { user: {}, attach: true })
  await flushPromises()
  return rendered
}

function consentItem(name: string): HTMLElement {
  const item = [...document.querySelectorAll<HTMLElement>('.acc-consent')].find((candidate) =>
    candidate.querySelector('.acc-consent__name')?.textContent.includes(name),
  )
  if (!item) throw new Error(`No consent of "${name}"`)
  return item
}

describe('ConsentsSection', () => {
  it('lists the consents of the household and offers to withdraw only the ones given', async () => {
    serveConsents([
      buildConsentResponse(),
      buildConsentResponse({
        personId: 'user-1',
        personName: 'Ada Lovelace',
        accepted: false,
        withdrawn: true,
      }),
      buildConsentResponse({ personId: 'child-2', personName: 'Anne King', accepted: false }),
    ])
    const { wrapper } = await renderSection()

    await vi.waitFor(() => expect(wrapper.findAll('.acc-consent')).toHaveLength(3))
    const action = t('pages.account.consents.withdraw.action')
    const given = consentItem('Byron Lovelace')
    expect(given.textContent).toContain('Uso de imágenes')
    expect(given.textContent).toContain('Hackathon de Primavera')
    expect(given.textContent).toContain(
      t('pages.account.consents.decided', {
        name: 'Ada Lovelace',
        date: formatDateTime('2026-05-01T09:00:00Z'),
      }),
    )
    expect(given.textContent).toContain(t('pages.account.consents.given'))
    expect(findButtons(action, given)).toHaveLength(1)
    const withdrawn = consentItem('Ada Lovelace')
    expect(withdrawn.textContent).toContain(t('pages.account.consents.withdrawn'))
    expect(findButtons(action, withdrawn)).toHaveLength(0)
    const notGiven = consentItem('Anne King')
    expect(notGiven.textContent).toContain(t('pages.account.consents.notGiven'))
    expect(findButtons(action, notGiven)).toHaveLength(0)
  })

  it('withdraws a consent of a minor once confirmed, reminding it is theirs from 14', async () => {
    const consents = serveConsents([buildConsentResponse()])
    const posted: string[] = []
    server.use(
      http.post(
        '/api/events/:eventId/terms/decisions/:userId/:termsDocumentId/withdraw',
        ({ request }) => {
          posted.push(new URL(request.url).pathname)
          return noContent()
        },
      ),
    )
    await renderSection()

    await click(findButton(t('pages.account.consents.withdraw.action')))
    expect(messageBox().textContent).toContain(
      t('pages.account.consents.withdraw.messageMinor', {
        document: 'Uso de imágenes',
        person: 'Byron Lovelace',
        event: 'Hackathon de Primavera',
      }),
    )
    const loads = consents.loads()
    await acceptMessageBox()

    await expectNotification(t('pages.account.consents.withdraw.done'))
    expect(posted).toEqual(['/api/events/event-1/terms/decisions/child-1/terms-1/withdraw'])
    expect(consents.loads()).toBeGreaterThan(loads)
  })

  it('asks about the own consent in the first person', async () => {
    serveConsents([buildConsentResponse({ personId: 'user-1', personName: 'Ada Lovelace' })])
    await renderSection()

    await click(findButton(t('pages.account.consents.withdraw.action')))

    expect(messageBox().textContent).toContain(
      t('pages.account.consents.withdraw.messageSelf', {
        document: 'Uso de imágenes',
        person: 'Ada Lovelace',
        event: 'Hackathon de Primavera',
      }),
    )
  })

  it('reports a withdrawal the API refuses', async () => {
    serveConsents([buildConsentResponse()])
    server.use(
      http.post('/api/events/:eventId/terms/decisions/:userId/:termsDocumentId/withdraw', () =>
        apiError(409, 'EventTermsConsentNotGiven'),
      ),
    )
    await renderSection()

    await click(findButton(t('pages.account.consents.withdraw.action')))
    await acceptMessageBox()

    await expectNotification(t('errors.EventTermsConsentNotGiven'))
  })

  it('tells when there are no consents or they cannot be loaded', async () => {
    serveConsents([])
    const { wrapper } = await renderSection()
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.account.consents.empty')))

    server.use(http.get('/api/me/consents', () => apiError(500, 'UnexpectedError')))
    const failed = await renderSection()
    await vi.waitFor(() =>
      expect(failed.wrapper.text()).toContain(t('pages.account.consents.error')),
    )
  })
})
