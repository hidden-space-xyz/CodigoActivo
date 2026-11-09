import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { BlockedDataPage } from '@/pages/admin/blocked-data'
import ConsultationPanel from '@/pages/admin/blocked-data/ui/ConsultationPanel.vue'
import OpenConsultationDialog from '@/pages/admin/blocked-data/ui/OpenConsultationDialog.vue'
import type {
  BlockedDataAccessResponse,
  BlockedDataConsultationResponse,
  BlockedDataExport,
  BlockedDataHoldResponse,
  BlockedDataSearch,
} from '@/shared/api/generated/models'
import { formatDateTime, toDateOnly } from '@/shared/lib/date'
import { ColumnFilterDate } from '@/shared/ui/column-filter'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import {
  acceptMessageBox,
  click,
  expectNotification,
  findButton,
  messageBox,
  notificationTexts,
  typeInto,
} from '../../../../../support/dom'

const COURT = 'Juzgado de León'
const CLAIMANT = 'Persona reclamante'

const CONSULTATION: BlockedDataConsultationResponse = {
  id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b01',
  officerId: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b02',
  officerName: 'Dana Delegada',
  requester: COURT,
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
  requester: COURT,
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

const HOLD: BlockedDataHoldResponse = {
  id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4b04',
  requester: CLAIMANT,
  reference: 'Reclamación 7/2026',
  accounts: 2,
  placedAt: '2026-10-25T09:05:00Z',
  placedByName: 'Dana Delegada',
}

function serve(
  access: BlockedDataAccessResponse,
  list: BlockedDataConsultationResponse[] = [],
  holds: BlockedDataHoldResponse[] = [],
) {
  const bodies: Record<string, unknown[]> = {
    code: [],
    open: [],
    search: [],
    extract: [],
    hold: [],
    lift: [],
  }
  server.use(
    http.get('/api/blocked-data/access', () => HttpResponse.json(access)),
    http.get('/api/blocked-data/consultations', () => HttpResponse.json(paged(list))),
    http.get('/api/blocked-data/holds', () => HttpResponse.json(paged(holds))),
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
    http.post(`/api/blocked-data/consultations/${CONSULTATION.id}/holds`, async ({ request }) => {
      bodies.hold!.push(await request.json())
      return HttpResponse.json({ ...HOLD, reference: CONSULTATION.reference }, { status: 201 })
    }),
    http.post(
      `/api/blocked-data/consultations/${CONSULTATION.id}/holds/:holdId/lift`,
      ({ params }) => {
        bodies.lift!.push(params.holdId)
        return new HttpResponse(null, { status: 204 })
      },
    ),
  )
  return bodies
}

async function openAndFind(wrapper: Awaited<ReturnType<typeof renderPage>>['wrapper']) {
  await click(findButton(t('pages.admin.blockedData.open.action')))
  await fillDialog(COURT, CONSULTATION.reference, '123456')
  await vi.waitFor(() => expect(document.querySelector('#blocked-query')).not.toBeNull())
  await typeInto('#blocked-query', 'marta@example.test')
  await click(findButton(t('pages.admin.blockedData.consultation.searchAction')))
  await vi.waitFor(() => expect(wrapper.text()).toContain('Marta Miembro'))
}

function liftButton(): HTMLButtonElement {
  const button = document.querySelector<HTMLButtonElement>('[data-testid="row-action-lift"]')
  if (!button) throw new Error('No lift action is shown')
  return button
}

async function fillDialog(
  requester: string,
  reference: string,
  code: string,
  next = t('pages.admin.blockedData.open.continue'),
) {
  await typeInto('#blocked-requester', requester)
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
    user: { staffRole: 'Administrator', email: 'dpd@example.test', twoFactorMethod },
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
    expect(wrapper.text()).toContain(COURT)
    expect(wrapper.text()).not.toContain(t('pages.admin.blockedData.open.action'))
  })

  it('emails the code to a designated account without an authenticator app', async () => {
    const bodies = serve(OFFICER)
    const { wrapper } = await renderPage('Email')

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(COURT, 'Diligencias 123/2026', '123456', t('common.sendCode'))
    await vi.waitFor(() => expect(bodies.open).toHaveLength(1))

    expect(bodies.code).toEqual([{ currentPassword: 'secret' }])
    expect(bodies.open).toEqual([
      {
        requester: COURT,
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
    await typeInto('#blocked-requester', CLAIMANT)
    await typeInto('#blocked-reference', 'Ref')
    await typeInto('#blocked-password', 'wrong')
    await click(findButton(t('common.sendCode')))
    await vi.waitFor(() => expect(dialog.props('error')).not.toBe(''))

    expect(dialog.props('error')).toBe(t('errors.UserCurrentPasswordIncorrect'))
    expect(dialog.props('codeStep')).toBe(false)
  })

  it('opens a consultation, finds an account and downloads its extract', async () => {
    const bodies = serve(OFFICER)
    const files = saved()
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(COURT, 'Diligencias 123/2026', '123456')
    await vi.waitFor(() => expect(bodies.open).toHaveLength(1))

    expect(bodies.open).toEqual([
      {
        requester: COURT,
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
      destinatario: COURT,
      entradas: [{ tipo: t('pages.admin.blockedData.entities.User'), legible: true }],
    })
  })

  it('emails another code for the same request after one is rejected and clears the rejection', async () => {
    const bodies = serve(OFFICER)
    server.use(
      http.post('/api/blocked-data/consultations', () => apiError(400, 'TwoFactorCodeInvalid')),
    )
    const { wrapper } = await renderPage('Email')
    const dialog = wrapper.findComponent(OpenConsultationDialog)

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(COURT, 'Ref', '000000', t('common.sendCode'))
    await vi.waitFor(() => expect(dialog.props('error')).toBe(t('errors.TwoFactorCodeInvalid')))

    await click(findButton(t('common.resendCode')))

    await vi.waitFor(() => expect(bodies.code).toHaveLength(2))
    expect(bodies.code).toEqual([{ currentPassword: 'secret' }, { currentPassword: 'secret' }])
    expect(dialog.props('error')).toBe('')
    expect(dialog.props('codeStep')).toBe(true)
  })

  it('narrows the extract to the chosen period and kinds of data', async () => {
    const bodies = serve(OFFICER)
    saved()
    const { wrapper } = await renderPage()
    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(COURT, 'Ref', '123456')
    await vi.waitFor(() => expect(document.querySelector('#blocked-query')).not.toBeNull())
    await typeInto('#blocked-query', 'marta@example.test')
    await click(findButton(t('pages.admin.blockedData.consultation.searchAction')))
    await vi.waitFor(() => expect(wrapper.text()).toContain('Marta Miembro'))

    const panel = wrapper.findComponent(ConsultationPanel)
    panel
      .findComponent(ColumnFilterDate)
      .vm.$emit('update:modelValue', [new Date(2026, 0, 1), new Date(2026, 5, 30)])
    panel.findComponent(ElSelect).vm.$emit('update:modelValue', ['User'])
    await flushPromises()
    await click(findButton(t('pages.admin.blockedData.consultation.download')))
    await vi.waitFor(() => expect(bodies.extract).toHaveLength(1))

    expect(bodies.extract).toEqual([
      { accountId: ACCOUNT, from: '2026-01-01', to: '2026-06-30', entities: ['User'] },
    ])
  })

  it('shows why a consultation could not be opened inside the dialog', async () => {
    serve(OFFICER)
    server.use(
      http.post('/api/blocked-data/consultations', () => apiError(400, 'TwoFactorCodeInvalid')),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(CLAIMANT, 'Ref', '000000')
    await vi.waitFor(() =>
      expect(wrapper.findComponent(OpenConsultationDialog).props('error')).not.toBe(''),
    )

    expect(wrapper.findComponent(OpenConsultationDialog).props('error')).toBe(
      t('errors.TwoFactorCodeInvalid'),
    )
    expect(wrapper.text()).not.toContain(t('pages.admin.blockedData.consultation.title'))
  })

  it('lists the holds to any administrator, without the action to lift one', async () => {
    serve(
      { isPrivacyOfficer: false, entities: [] },
      [],
      [
        HOLD,
        { ...HOLD, id: 'h2', reference: 'Diligencias 9/2025', liftedAt: '2026-10-30T12:00:00Z' },
      ],
    )

    const { wrapper } = await renderPage()

    const holds = wrapper.find('[data-testid="blocked-data-holds"]')
    expect(holds.text()).toContain('Reclamación 7/2026')
    expect(holds.text()).toContain(CLAIMANT)
    expect(holds.text()).toContain(t('pages.admin.blockedData.holds.active'))
    expect(holds.text()).toContain('Dana Delegada')
    expect(holds.text()).toContain(
      t('pages.admin.blockedData.holds.liftedByErased', {
        date: formatDateTime('2026-10-30T12:00:00Z'),
      }),
    )
    expect(holds.text()).not.toContain(t('pages.admin.blockedData.holds.liftHint'))
    expect(document.querySelector('[data-testid="row-action-lift"]')).toBeNull()
  })

  it('keeps the history of the picked account once the officer confirms', async () => {
    const bodies = serve(OFFICER)
    const { wrapper } = await renderPage()
    await openAndFind(wrapper)

    await click(findButton(t('pages.admin.blockedData.holds.place.action')))

    expect(messageBox().textContent).toContain(
      t('pages.admin.blockedData.holds.place.message', {
        name: 'Marta Miembro',
        reference: CONSULTATION.reference,
      }),
    )
    expect(bodies.hold).toEqual([])

    await acceptMessageBox()
    await vi.waitFor(() => expect(bodies.hold).toHaveLength(1))

    expect(bodies.hold).toEqual([{ accountId: ACCOUNT }])
    await expectNotification(
      t('pages.admin.blockedData.holds.place.done', { reference: CONSULTATION.reference }),
    )
  })

  it('lifts an active hold only from an open consultation, once the officer confirms', async () => {
    const bodies = serve(OFFICER, [], [HOLD])
    const { wrapper } = await renderPage()

    expect(wrapper.text()).toContain(t('pages.admin.blockedData.holds.liftHint'))
    expect(liftButton().disabled).toBe(true)

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(COURT, 'Sentencia firme 1/2027', '123456')
    await vi.waitFor(() => expect(liftButton().disabled).toBe(false))
    expect(wrapper.text()).not.toContain(t('pages.admin.blockedData.holds.liftHint'))

    await click(liftButton())
    expect(messageBox().textContent).toContain(
      t('pages.admin.blockedData.holds.lift.message', { reference: HOLD.reference }),
    )
    await acceptMessageBox()
    await vi.waitFor(() => expect(bodies.lift).toHaveLength(1))

    expect(bodies.lift).toEqual([HOLD.id])
    await expectNotification(t('pages.admin.blockedData.holds.lift.done'))
  })

  it('never offers to lift a hold that was lifted already', async () => {
    serve(OFFICER, [], [{ ...HOLD, liftedAt: '2026-10-30T12:00:00Z', liftedByName: 'Leo' }])
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.blockedData.open.action')))
    await fillDialog(COURT, 'Ref', '123456')
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.blockedData.consultation.title')),
    )

    expect(liftButton().disabled).toBe(true)
    expect(wrapper.text()).toContain(
      t('pages.admin.blockedData.holds.lifted', {
        date: formatDateTime('2026-10-30T12:00:00Z'),
        name: 'Leo',
      }),
    )
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
    await fillDialog(COURT, 'Ref', '123456')
    await vi.waitFor(() => expect(document.querySelector('#blocked-query')).not.toBeNull())

    await typeInto('#blocked-query', 'marta')
    await click(findButton(t('pages.admin.blockedData.consultation.searchAction')))
    await vi.waitFor(() => expect(notificationTexts()).not.toEqual([]))

    expect(wrapper.text()).not.toContain(t('pages.admin.blockedData.consultation.title'))
    expect(notificationTexts().join(' ')).toContain(t('errors.BlockedDataConsultationExpired'))
  })
})
