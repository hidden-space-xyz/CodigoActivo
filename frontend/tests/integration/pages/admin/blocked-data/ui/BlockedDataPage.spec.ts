import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { BlockedDataPage } from '@/pages/admin/blocked-data'
import OpenConsultationDialog from '@/pages/admin/blocked-data/ui/OpenConsultationDialog.vue'
import type {
  BlockedDataAccessResponse,
  BlockedDataConsultationResponse,
  BlockedDataExport,
  BlockedDataSearch,
} from '@/shared/api/generated/models'
import { toDateOnly } from '@/shared/lib/date'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import { click, findButton, notificationTexts, typeInto } from '../../../../../support/dom'

const CONSULTATION: BlockedDataConsultationResponse = {
  id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b01',
  officerId: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b02',
  officerName: 'Dana Delegada',
  authority: 'Court',
  reference: 'Diligencias 123/2026',
  openedAt: '2026-10-25T09:00:00Z',
  expiresAt: '2026-10-25T09:15:00Z',
  foundAccounts: 0,
  extracts: 0,
}

const ACCOUNT = '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b03'

const FOUND: BlockedDataSearch = {
  candidates: [
    {
      accountId: ACCOUNT,
      firstName: 'Marta',
      lastName: 'Miembro',
      exists: false,
      erasedAt: '2026-10-01T10:00:00Z',
      entries: [{ entity: 'User', count: 1, firstAt: 'x', lastAt: 'x' }],
    },
  ],
}

const EXTRACT: BlockedDataExport = {
  generatedAt: '2026-10-25T09:05:00Z',
  authority: 'Court',
  reference: 'Diligencias 123/2026',
  accountId: ACCOUNT,
  entities: [],
  entries: [
    {
      id: 'e1',
      entity: 'User',
      occurredAt: '2026-10-01T10:00:00Z',
      readable: true,
      operation: 'Deleted',
      command: 'DeleteUser',
      values: { Email: 'marta@example.test' },
    },
  ],
}

function serve(access: BlockedDataAccessResponse, list: BlockedDataConsultationResponse[] = []) {
  const bodies: Record<string, unknown[]> = { code: [], open: [], search: [], extract: [] }
  server.use(
    http.get('/api/blocked-data/access', () => HttpResponse.json(access)),
    http.get('/api/blocked-data/consultations', () => HttpResponse.json(paged(list))),
    http.post('/api/blocked-data/consultations/code', async ({ request }) => {
      bodies.code!.push(await request.json())
      return new HttpResponse(null, { status: 204 })
    }),
    http.post('/api/blocked-data/consultations', async ({ request }) => {
      bodies.open!.push(await request.json())
      return HttpResponse.json(CONSULTATION, { status: 201 })
    }),
    http.post(
      `/api/blocked-data/consultations/${CONSULTATION.id}/searches`,
      async ({ request }) => {
        bodies.search!.push(await request.json())
        return HttpResponse.json(FOUND)
      },
    ),
    http.post(
      `/api/blocked-data/consultations/${CONSULTATION.id}/extracts`,
      async ({ request }) => {
        bodies.extract!.push(await request.json())
        return HttpResponse.json(EXTRACT)
      },
    ),
  )
  return bodies
}

async function fillDialog(
  wrapper: Awaited<ReturnType<typeof renderPage>>['wrapper'],
  authority: string,
  reference: string,
  code: string,
  next = t('pages.admin.blockedData.open.continue'),
) {
  wrapper
    .findComponent(OpenConsultationDialog)
    .findComponent(ElSelect)
    .vm.$emit('update:modelValue', authority)
  await typeInto('#blocked-reference', reference)
  await typeInto('#blocked-password', 'secret')
  await click(findButton(next))
  await vi.waitFor(() => expect(document.querySelector('#blocked-code')).not.toBeNull())
  await typeInto('#blocked-code', code)
  await click(findButton(t('pages.admin.blockedData.open.confirm')))
}

async function renderPage(twoFactorMethod: 'Email' | 'Authenticator' = 'Authenticator') {
  const rendered = await renderWithProviders(BlockedDataPage, {
    route: '/admin/blocked-data',
    attach: true,
    user: { isAdmin: true, email: 'dpd@example.test', twoFactorMethod },
  })
  await flushPromises()
  return rendered
}

function saved() {
  const blobs: Blob[] = []
  const names: string[] = []
  vi.spyOn(URL, 'createObjectURL').mockImplementation((blob) => {
    blobs.push(blob as Blob)
    return 'blob:extract'
  })
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
    this: HTMLAnchorElement,
  ) {
    names.push(this.download)
  })
  return { blobs, names }
}

const OFFICER = { isPrivacyOfficer: true, entities: ['User'] }

describe('BlockedDataPage', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('lists the consultations to any administrator, without the action to open one', async () => {
    serve({ isPrivacyOfficer: false, entities: [] }, [
      { ...CONSULTATION, foundAccounts: 2, extracts: 1 },
    ])

    const { wrapper } = await renderPage()

    expect(wrapper.text()).toContain('Dana Delegada')
    expect(wrapper.text()).toContain('Diligencias 123/2026')
    expect(wrapper.text()).toContain(t('pages.admin.blockedData.authorities.Court'))
    expect(wrapper.text()).not.toContain(t('pages.admin.blockedData.open.action'))
  })

  it('emails the code to a designated account without an authenticator app', async () => {
    const bodies = serve(OFFICER)
    const { wrapper } = await renderPage('Email')

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(
      wrapper,
      'Court',
      'Diligencias 123/2026',
      '123456',
      t('pages.admin.blockedData.open.sendCode'),
    )
    await vi.waitFor(() => expect(bodies.open).toHaveLength(1))

    expect(bodies.code).toEqual([{ currentPassword: 'secret' }])
    expect(bodies.open).toEqual([
      {
        authority: 'Court',
        reference: 'Diligencias 123/2026',
        currentPassword: 'secret',
        code: '123456',
      },
    ])
    expect(wrapper.text()).toContain(t('pages.admin.blockedData.consultation.title'))
  })

  it('keeps the first step when the code could not be emailed', async () => {
    serve(OFFICER)
    server.use(
      http.post('/api/blocked-data/consultations/code', () =>
        apiError(400, 'UserCurrentPasswordIncorrect'),
      ),
    )
    const { wrapper } = await renderPage('Email')
    const dialog = wrapper.findComponent(OpenConsultationDialog)

    await click(findButton(t('pages.admin.blockedData.open.action')))
    dialog.findComponent(ElSelect).vm.$emit('update:modelValue', 'Claim')
    await typeInto('#blocked-reference', 'Ref')
    await typeInto('#blocked-password', 'wrong')
    await click(findButton(t('pages.admin.blockedData.open.sendCode')))
    await vi.waitFor(() => expect(dialog.props('error')).not.toBe(''))

    expect(dialog.props('error')).toBe(t('errors.UserCurrentPasswordIncorrect'))
    expect(dialog.props('codeStep')).toBe(false)
  })

  it('opens a consultation, finds an account and downloads its extract', async () => {
    const bodies = serve(OFFICER)
    const files = saved()
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(wrapper, 'Court', 'Diligencias 123/2026', '123456')
    await vi.waitFor(() => expect(bodies.open).toHaveLength(1))

    expect(bodies.open).toEqual([
      {
        authority: 'Court',
        reference: 'Diligencias 123/2026',
        currentPassword: 'secret',
        code: '123456',
      },
    ])
    expect(wrapper.text()).toContain(t('pages.admin.blockedData.consultation.title'))

    await typeInto('#blocked-query', ' ma ')
    expect(findButton(t('pages.admin.blockedData.consultation.searchAction')).disabled).toBe(true)

    await typeInto('#blocked-query', '  marta@example.test ')
    await click(findButton(t('pages.admin.blockedData.consultation.searchAction')))
    await vi.waitFor(() => expect(bodies.search).toHaveLength(1))

    expect(bodies.search).toEqual([{ query: 'marta@example.test' }])
    expect(wrapper.text()).toContain('Marta Miembro')

    await click(findButton(t('pages.admin.blockedData.consultation.download')))
    await vi.waitFor(() => expect(files.blobs).toHaveLength(1))

    expect(bodies.extract).toEqual([{ accountId: ACCOUNT, from: null, to: null, entities: [] }])
    expect(files.names).toEqual([
      t('pages.admin.blockedData.extract.fileName', { date: toDateOnly(new Date()) }),
    ])
    const file = JSON.parse(await files.blobs[0]!.text())
    expect(file).toMatchObject({
      referencia: 'Diligencias 123/2026',
      destinatario: t('pages.admin.blockedData.authorities.Court'),
      entradas: [{ tipo: t('pages.admin.blockedData.entities.User'), legible: true }],
    })
  })

  it('shows why a consultation could not be opened inside the dialog', async () => {
    serve(OFFICER)
    server.use(
      http.post('/api/blocked-data/consultations', () => apiError(400, 'TwoFactorCodeInvalid')),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(wrapper, 'Claim', 'Ref', '000000')
    await vi.waitFor(() =>
      expect(wrapper.findComponent(OpenConsultationDialog).props('error')).not.toBe(''),
    )

    expect(wrapper.findComponent(OpenConsultationDialog).props('error')).toBe(
      t('errors.TwoFactorCodeInvalid'),
    )
    expect(wrapper.text()).not.toContain(t('pages.admin.blockedData.consultation.title'))
  })

  it('closes the consultation once the API says it is over', async () => {
    serve(OFFICER)
    server.use(
      http.post(`/api/blocked-data/consultations/${CONSULTATION.id}/searches`, () =>
        apiError(403, 'BlockedDataConsultationExpired'),
      ),
    )
    const { wrapper } = await renderPage()
    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(wrapper, 'Court', 'Ref', '123456')
    await vi.waitFor(() => expect(document.querySelector('#blocked-query')).not.toBeNull())

    await typeInto('#blocked-query', 'marta')
    await click(findButton(t('pages.admin.blockedData.consultation.searchAction')))
    await vi.waitFor(() => expect(notificationTexts()).not.toEqual([]))

    expect(wrapper.text()).not.toContain(t('pages.admin.blockedData.consultation.title'))
    expect(notificationTexts().join(' ')).toContain(t('errors.BlockedDataConsultationExpired'))
  })
})
