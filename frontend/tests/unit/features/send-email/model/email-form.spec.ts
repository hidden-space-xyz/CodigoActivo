import { describe, expect, it } from 'vitest'

import {
  addAttachments,
  MAX_ATTACHMENTS,
  MAX_ATTACHMENTS_BYTES,
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
  it('starts blank', () => {
    expect(toEmailDraft()).toEqual({ subject: '', body: '', attachments: [] })
  })
})

describe('readEmailDraft', () => {
  it('sends the subject, body and attachments as typed', () => {
    const attachments = files(2)

    const reading = readEmailDraft({ subject: ' Welcome ', body: 'Hello\nthere', attachments })

    expect(reading.problems).toEqual({})
    expect(reading.value?.subject).toBe(' Welcome ')
    expect(reading.value?.body).toBe('Hello\nthere')
    expect(reading.value?.attachments.map((file) => file.name)).toEqual([
      'file-0.txt',
      'file-1.txt',
    ])
    expect(reading.value?.attachments).not.toBe(attachments)
  })

  it('requires a subject and a body that are not blank', () => {
    expect(readEmailDraft({ subject: '  ', body: '\n', attachments: [] })).toEqual({
      problems: {
        subject: 'features.sendEmail.form.problems.subjectRequired',
        body: 'features.sendEmail.form.problems.bodyRequired',
      },
      value: null,
    })
    expect(readEmailDraft({ subject: 'Hi', body: '', attachments: files(1) })).toEqual({
      problems: { body: 'features.sendEmail.form.problems.bodyRequired' },
      value: null,
    })
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
})
