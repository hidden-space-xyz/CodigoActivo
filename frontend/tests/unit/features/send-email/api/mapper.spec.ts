import { describe, expect, it } from 'vitest'

import { toEmailAudience, toEmailBody, toSendEmailResult } from '@/features/send-email/api/mapper'

describe('send email mapper', () => {
  it('maps the audience of an email', () => {
    expect(toEmailAudience({ recipients: 12, withoutConsent: 3 })).toEqual({
      recipients: 12,
      withoutConsent: 3,
    })
  })

  it('maps the outcome of a send', () => {
    expect(toSendEmailResult({ queued: 9, skipped: 1 })).toEqual({ queued: 9, skipped: 1 })
  })

  it('trims the subject and body and leaves out an empty attachment list', () => {
    expect(
      toEmailBody({ subject: '  Aviso  ', body: ' Hola ', purpose: 'Notice', attachments: [] }),
    ).toEqual({ subject: 'Aviso', body: 'Hola', purpose: 'Notice' })
  })

  it('sends the attachments when there are some', () => {
    const attachment = new File(['contenido'], 'programa.pdf', { type: 'application/pdf' })

    expect(
      toEmailBody({
        subject: 'Boletín',
        body: 'Novedades',
        purpose: 'News',
        attachments: [attachment],
      }),
    ).toEqual({ subject: 'Boletín', body: 'Novedades', purpose: 'News', attachments: [attachment] })
  })
})
