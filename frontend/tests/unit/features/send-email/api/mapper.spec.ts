import { describe, expect, it } from 'vitest'

import { toEmailBody } from '@/features/send-email/api/mapper'

describe('send email mapper', () => {
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
