import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useSendEmail, useSendEmailDialog } from '@/features/send-email'
import type { SendEmailPayload } from '@/features/send-email/model/useSendEmail'
import type { EmailAudience } from '@/features/send-email/model/types'
import type { SendEmailResultResponse } from '@/shared/api/generated/models'

import {
  expectNotification,
  notificationTexts,
  queryOf,
  tp,
  withSetup,
} from '../../../support/fixtures/admin-content/helpers'
import { apiError, http, HttpResponse, server } from '../../../support/server'

interface CapturedEmail {
  url: string
  subject: FormDataEntryValue | null
  body: FormDataEntryValue | null
  attachments: string[]
}

async function capture(request: Request): Promise<CapturedEmail> {
  const form = await request.formData()
  return {
    url: request.url,
    subject: form.get('subject'),
    body: form.get('body'),
    attachments: form.getAll('attachments').map((value) => (value as File).name),
  }
}

const payload: SendEmailPayload = { subject: '  Hello ', body: ' Body text  ', attachments: [] }

describe('useSendEmail', () => {
  it('emails one user with trimmed subject and body and no attachments', async () => {
    let received: CapturedEmail | undefined
    server.use(
      http.post('/api/emails/users/:userId', async ({ request }) => {
        received = await capture(request)
        return HttpResponse.json({ queued: 1 })
      }),
    )
    const { result } = await withSetup(() => useSendEmail())

    const response = await result.sendToUser.mutateAsync({ userId: 'user-7', payload })

    expect(response).toEqual({ queued: 1 })
    expect(received).toEqual({
      url: 'http://localhost:3000/api/emails/users/user-7',
      subject: 'Hello',
      body: 'Body text',
      attachments: [],
    })
  })

  it('emails filtered users with their attachments and the filter query', async () => {
    let received: CapturedEmail | undefined
    server.use(
      http.post('/api/emails/users', async ({ request }) => {
        received = await capture(request)
        return HttpResponse.json({ queued: 2, skipped: 1 })
      }),
    )
    const { result } = await withSetup(() => useSendEmail())

    await result.sendToUsers.mutateAsync({
      params: { name: 'ada', isAdmin: false },
      payload: {
        ...payload,
        attachments: [new File(['a'], 'a.txt'), new File(['b'], 'b.pdf')],
      },
    })

    expect(queryOf(received?.url ?? '')).toEqual({ name: 'ada', isAdmin: 'false' })
    expect(received?.attachments).toEqual(['a.txt', 'b.pdf'])
  })

  it('emails event attendees matching the report filters', async () => {
    let received: CapturedEmail | undefined
    server.use(
      http.post('/api/emails/events/:eventId/attendees', async ({ request }) => {
        received = await capture(request)
        return HttpResponse.json({ queued: 3 })
      }),
    )
    const { result } = await withSetup(() => useSendEmail())

    await result.sendToEventAttendees.mutateAsync({
      eventId: 'event-1',
      params: { search: 'ana' },
      payload,
    })

    expect(received?.url).toBe(
      'http://localhost:3000/api/emails/events/event-1/attendees?search=ana',
    )
    expect(received?.subject).toBe('Hello')
  })

  it('loads the audience of the filtered users and defaults missing counts', async () => {
    const urls: string[] = []
    let body: Record<string, number> = { recipients: 5, withoutConsent: 2 }
    server.use(
      http.get('/api/emails/users/audience', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(body)
      }),
    )
    const { result } = await withSetup(() => useSendEmail())

    await expect(result.usersAudience({ promotionalConsent: false })).resolves.toEqual({
      recipients: 5,
      withoutConsent: 2,
    })
    body = {}
    await expect(result.usersAudience({ id: 'user-7' })).resolves.toEqual({
      recipients: 0,
      withoutConsent: 0,
    })
    expect(urls.map(queryOf)).toEqual([{ promotionalConsent: 'false' }, { id: 'user-7' }])
  })

  it('loads the audience of the filtered event attendees', async () => {
    let url = ''
    server.use(
      http.get('/api/emails/events/:eventId/attendees/audience', ({ request }) => {
        url = request.url
        return HttpResponse.json({ recipients: 3, withoutConsent: 1 })
      }),
    )
    const { result } = await withSetup(() => useSendEmail())

    await expect(result.eventAttendeesAudience('event-1', { search: 'ana' })).resolves.toEqual({
      recipients: 3,
      withoutConsent: 1,
    })
    expect(url).toBe(
      'http://localhost:3000/api/emails/events/event-1/attendees/audience?search=ana',
    )
  })
})

interface Recipient {
  id?: string
  name: string
}

function setupDialog(
  overrides: {
    bulkPending?: () => boolean
    fetchAudience?: (recipient: Recipient | null) => Promise<EmailAudience>
  } = {},
) {
  const sendAll = vi.fn()
  const onError = vi.fn()
  const fetchAudience = vi.fn(
    overrides.fetchAudience ?? (() => Promise.resolve({ recipients: 1, withoutConsent: 0 })),
  )
  return withSetup(() =>
    useSendEmailDialog<Recipient>({
      idOf: (recipient) => recipient.id,
      targetOne: (recipient) => `one:${recipient.name}`,
      targetAll: () => 'everyone',
      bulkPending: overrides.bulkPending ?? (() => false),
      sendAll,
      fetchAudience,
      onError,
    }),
  ).then((rendered) => ({ ...rendered, sendAll, onError, fetchAudience }))
}

describe('useSendEmailDialog', () => {
  it('targets the whole audience or a single recipient when opened', async () => {
    const { result } = await setupDialog()

    expect(result.visible.value).toBe(false)
    expect(result.target.value).toBe('everyone')

    result.open({ id: 'user-1', name: 'Ada' })
    expect(result.visible.value).toBe(true)
    expect(result.recipient.value).toEqual({ id: 'user-1', name: 'Ada' })
    expect(result.target.value).toBe('one:Ada')

    result.open(null)
    expect(result.target.value).toBe('everyone')
  })

  it('loads the audience of whoever the dialog targets when it opens', async () => {
    const { result, fetchAudience } = await setupDialog({
      fetchAudience: (recipient) =>
        Promise.resolve({ recipients: 2, withoutConsent: recipient ? 0 : 2 }),
    })

    expect(result.withoutConsent.value).toBeNull()
    result.open(null)
    expect(result.withoutConsent.value).toBeNull()
    await flushPromises()
    expect(result.withoutConsent.value).toBe(2)

    result.open({ id: 'user-1', name: 'Ada' })
    expect(result.withoutConsent.value).toBeNull()
    await flushPromises()
    expect(result.withoutConsent.value).toBe(0)
    expect(fetchAudience.mock.calls).toEqual([[null], [{ id: 'user-1', name: 'Ada' }]])
  })

  it('ignores an audience answered after the dialog was opened again', async () => {
    const pending: ((audience: EmailAudience) => void)[] = []
    const { result } = await setupDialog({
      fetchAudience: () =>
        new Promise<EmailAudience>((resolve) => {
          pending.push(resolve)
        }),
    })

    result.open(null)
    result.open({ id: 'user-1', name: 'Ada' })
    pending[1]?.({ recipients: 1, withoutConsent: 1 })
    await flushPromises()
    pending[0]?.({ recipients: 9, withoutConsent: 9 })
    await flushPromises()

    expect(result.withoutConsent.value).toBe(1)
  })

  it('leaves the audience unknown when it cannot be loaded or has no recipient id', async () => {
    const { result, fetchAudience } = await setupDialog({
      fetchAudience: () => Promise.reject(new Error('offline')),
    })

    result.open(null)
    await flushPromises()
    expect(result.withoutConsent.value).toBeNull()

    result.open({ name: 'Nobody' })
    await flushPromises()
    expect(result.withoutConsent.value).toBeNull()
    expect(fetchAudience).toHaveBeenCalledTimes(1)
  })

  it('reports queued and skipped counts and closes after a send to one user', async () => {
    let release: (() => void) | undefined
    server.use(
      http.post('/api/emails/users/:userId', async () => {
        await new Promise<void>((resolve) => {
          release = resolve
        })
        return HttpResponse.json({ queued: 1, skipped: 3 })
      }),
    )
    const { result } = await setupDialog()

    result.open({ id: 'user-1', name: 'Ada' })
    result.submit(payload)
    await vi.waitFor(() => expect(release).toBeDefined())
    expect(result.sending.value).toBe(true)
    release?.()

    await vi.waitFor(() => expect(result.visible.value).toBe(false))
    await expectNotification(tp('features.sendEmail.toast.queued', 1, { count: 1 }))
    await expectNotification(tp('features.sendEmail.toast.skipped', 3, { count: 3 }))
    expect(result.sending.value).toBe(false)
  })

  it('keeps the dialog open when nothing was queued', async () => {
    server.use(http.post('/api/emails/users/:userId', () => HttpResponse.json({})))
    const { result } = await setupDialog()

    result.open({ id: 'user-1', name: 'Ada' })
    result.submit(payload)
    await flushPromises()
    await vi.waitFor(() => expect(result.sending.value).toBe(false))

    expect(result.visible.value).toBe(true)
    expect(notificationTexts()).toEqual([])
  })

  it('passes send failures to the error handler', async () => {
    server.use(http.post('/api/emails/users/:userId', () => apiError(500)))
    const { result, onError } = await setupDialog()

    result.open({ id: 'user-1', name: 'Ada' })
    result.submit(payload)

    await vi.waitFor(() => expect(onError).toHaveBeenCalledTimes(1))
    expect(result.visible.value).toBe(true)
  })

  it('skips the single send when the recipient has no id', async () => {
    const { result, sendAll } = await setupDialog()

    result.open({ name: 'Nobody' })
    result.submit(payload)
    await flushPromises()

    expect(sendAll).not.toHaveBeenCalled()
    expect(result.sending.value).toBe(false)
  })

  it('delegates bulk sends to the page with result handlers', async () => {
    const { result, sendAll, onError } = await setupDialog({ bulkPending: () => true })

    expect(result.sending.value).toBe(true)
    result.open(null)
    result.submit(payload)

    expect(sendAll).toHaveBeenCalledTimes(1)
    const [sentPayload, handlers] = sendAll.mock.calls[0] as [
      SendEmailPayload,
      { onSuccess: (r: SendEmailResultResponse) => void; onError: (e: unknown) => void },
    ]
    expect(sentPayload).toBe(payload)
    expect(handlers.onError).toBe(onError)

    handlers.onSuccess({ queued: 4 })
    expect(result.visible.value).toBe(false)
    await expectNotification(tp('features.sendEmail.toast.queued', 4, { count: 4 }))
  })
})
