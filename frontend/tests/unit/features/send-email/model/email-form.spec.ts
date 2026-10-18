import { describe, expect, it } from 'vitest'

import {
  addAttachments,
  MAX_ATTACHMENTS,
  MAX_ATTACHMENTS_BYTES,
  reachedBy,
  readEmailDraft,
  toEmailDraft,
} from '@/features/send-email/model/email-form'

function files(count: number, prefix = 'file'): File[] {
  return Array.from({ length: count }, (_, index) => new File(['x'], `${prefix}-${index}.txt`))
}

function outcomeOf(added: ReturnType<typeof addAttachments>): string | string[] {
  return 'problem' in added ? added.problem : added.attachments.map((file) => file.name)
}

describe('toEmailDraft', () => {
  it('starts blank, with no purpose chosen', () => {
    expect(toEmailDraft()).toEqual({ purpose: null, subject: '', body: '', attachments: [] })
  })
})

describe('reachedBy', () => {
  const audience = { recipients: 5, withoutConsent: 2 }

  it('leaves out the recipients without consent only from a news email', () => {
    expect(reachedBy(audience, 'News')).toBe(3)
    expect(reachedBy(audience, 'Notice')).toBe(5)
    expect(reachedBy(audience, null)).toBe(5)
  })
})

describe('readEmailDraft', () => {
  it('sends the purpose, subject, body and attachments as typed', () => {
    const attachments = files(2)

    const reading = readEmailDraft({
      purpose: 'News',
      subject: ' Welcome ',
      body: 'Hello\nthere',
      attachments,
    })

    expect(reading.problems).toEqual({})
    expect(reading.value?.purpose).toBe('News')
    expect(reading.value?.subject).toBe(' Welcome ')
    expect(reading.value?.body).toBe('Hello\nthere')
    expect(reading.value?.attachments.map((file) => file.name)).toEqual([
      'file-0.txt',
      'file-1.txt',
    ])
    expect(reading.value?.attachments).not.toBe(attachments)
  })

  it('requires choosing whether the email is notice or news', () => {
    expect(
      readEmailDraft({ purpose: null, subject: 'Hi', body: 'Hello', attachments: [] }),
    ).toEqual({
      problems: { purpose: 'features.sendEmail.form.problems.purposeRequired' },
      value: null,
    })
  })

  it('requires a subject and a body that are not blank', () => {
    expect(
      readEmailDraft({ purpose: 'Notice', subject: '  ', body: '\n', attachments: [] }),
    ).toEqual({
      problems: {
        subject: 'features.sendEmail.form.problems.subjectRequired',
        body: 'features.sendEmail.form.problems.bodyRequired',
      },
      value: null,
    })
    expect(
      readEmailDraft({ purpose: 'Notice', subject: 'Hi', body: '', attachments: files(1) }),
    ).toEqual({
      problems: { body: 'features.sendEmail.form.problems.bodyRequired' },
      value: null,
    })
  })

  it('refuses a subject holding a tab or a line break', () => {
    for (const subject of ['Hola\tequipo', 'Hola\nequipo']) {
      expect(
        readEmailDraft({ purpose: 'Notice', subject, body: 'Hello', attachments: [] }),
      ).toEqual({
        problems: { subject: 'features.sendEmail.form.problems.subjectSingleLine' },
        value: null,
      })
    }
  })
})

describe('addAttachments', () => {
  it('appends the picked files to the attached ones', () => {
    const current = files(1, 'current')

    expect(outcomeOf(addAttachments(current, files(2, 'picked')))).toEqual([
      'current-0.txt',
      'picked-0.txt',
      'picked-1.txt',
    ])
    expect(current).toHaveLength(1)
  })

  it('accepts up to the maximum number of files and refuses more', () => {
    expect(outcomeOf(addAttachments(files(MAX_ATTACHMENTS - 1), files(1)))).toHaveLength(
      MAX_ATTACHMENTS,
    )
    expect(outcomeOf(addAttachments(files(MAX_ATTACHMENTS - 1), files(2)))).toBe('tooMany')
  })

  it('accepts up to the maximum combined size and refuses more', () => {
    const limit = new File([new Uint8Array(MAX_ATTACHMENTS_BYTES)], 'big.bin')

    expect(outcomeOf(addAttachments([], [limit]))).toEqual(['big.bin'])
    expect(outcomeOf(addAttachments([limit], files(1)))).toBe('tooLarge')
  })

  it('refuses an empty file, which the API would not send', () => {
    const empty = new File([], 'empty.txt')

    expect(outcomeOf(addAttachments(files(1), [empty, ...files(1, 'picked')]))).toBe('empty')
  })
})
