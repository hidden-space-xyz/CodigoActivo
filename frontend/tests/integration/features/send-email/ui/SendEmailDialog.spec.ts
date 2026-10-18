import { flushPromises } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { SendEmailDialog } from '@/features/send-email'
import type { EmailPurpose, SendEmailPayload } from '@/features/send-email/model/types'
import { MAX_ATTACHMENTS, MAX_ATTACHMENTS_BYTES } from '@/features/send-email/model/email-form'

import { renderWithProviders, t } from '../../../../support/render'
import {
  acceptMessageBox,
  cancelMessageBox,
  click,
  findButton,
  findButtons,
  inputValue,
  messageBox,
  openDialog,
  pickFiles,
  tp,
  typeInto,
} from '../../../../support/dom'
import { formatFileSize } from '@/shared/lib/number'

const TITLE = 'features.sendEmail.header'

async function renderDialog(
  props: {
    target?: string
    sending?: boolean
    recipients?: number | null
    withoutConsent?: number | null
  } = {},
) {
  const rendered = await renderWithProviders(SendEmailDialog, {
    props: {
      visible: false,
      target: props.target ?? 'Ada Lovelace',
      sending: props.sending ?? false,
      recipients: props.recipients ?? null,
      withoutConsent: props.withoutConsent ?? null,
    },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return { ...rendered, dialog: openDialog(t(TITLE)) }
}

function fileInput(dialog: HTMLElement): HTMLInputElement {
  const input = dialog.querySelector<HTMLInputElement>('input[type="file"]')
  if (!input) throw new Error('missing file input')
  return input
}

async function choosePurpose(dialog: HTMLElement, purpose: EmailPurpose): Promise<void> {
  const radio = dialog.querySelector<HTMLInputElement>(`input[type="radio"][value="${purpose}"]`)
  if (!radio) throw new Error(`missing ${purpose} option`)
  await click(radio)
}

async function compose(dialog: HTMLElement, purpose: EmailPurpose = 'Informational') {
  await choosePurpose(dialog, purpose)
  await typeInto('#send-email-subject', 'Asunto')
  await typeInto('#send-email-body', 'Mensaje')
}

function files(count: number, size = 10): File[] {
  return Array.from(
    { length: count },
    (_, index) => new File(['x'.repeat(size)], `file-${index}.txt`),
  )
}

describe('SendEmailDialog', () => {
  it('leaves the recipients without consent out of a promotional email and says how many', async () => {
    const { wrapper, dialog } = await renderDialog({ recipients: 5, withoutConsent: 2 })

    expect(dialog.textContent).toContain(tp('features.sendEmail.recipients', 5, { count: 5 }))
    expect(dialog.querySelector('.el-alert')).toBeNull()

    await compose(dialog, 'Promotional')

    expect(dialog.textContent).toContain(
      tp('features.sendEmail.promotionalRecipients', 3, { count: 3 }),
    )
    expect(dialog.textContent).toContain(t('features.sendEmail.purpose.promotionalHint'))
    const alert = dialog.querySelector('.el-alert')
    expect(alert?.classList).toContain('el-alert--info')
    expect(alert?.querySelector('.el-alert__close-btn')).toBeNull()
    expect(alert?.textContent).toContain(tp('features.sendEmail.withoutConsent', 2, { count: 2 }))

    await click(findButton(t('features.sendEmail.send'), dialog))
    expect(messageBox().textContent).toContain(
      tp('features.sendEmail.confirm.messageCount', 3, { count: 3 }),
    )
    await acceptMessageBox()

    const emitted = wrapper.emitted('submit')?.[0]?.[0] as SendEmailPayload | undefined
    expect(emitted?.purpose).toBe('Promotional')
  })

  it('uses the singular note for one recipient without consent', async () => {
    const { dialog } = await renderDialog({ recipients: 3, withoutConsent: 1 })

    await choosePurpose(dialog, 'Promotional')

    expect(dialog.querySelector('.el-alert')?.textContent).toContain(
      tp('features.sendEmail.withoutConsent', 1, { count: 1 }),
    )
  })

  it('reaches everyone with an informational email', async () => {
    const { dialog } = await renderDialog({ recipients: 5, withoutConsent: 2 })

    await choosePurpose(dialog, 'Informational')

    expect(dialog.textContent).toContain(tp('features.sendEmail.recipients', 5, { count: 5 }))
    expect(dialog.textContent).toContain(t('features.sendEmail.purpose.informationalHint'))
    expect(dialog.querySelector('.el-alert')).toBeNull()
  })

  it('blocks a promotional email when nobody accepted promotional communications', async () => {
    const { wrapper, dialog } = await renderDialog({ recipients: 1, withoutConsent: 1 })

    await compose(dialog, 'Promotional')

    expect(dialog.textContent).toContain(
      tp('features.sendEmail.promotionalRecipients', 0, { count: 0 }),
    )
    expect(findButton(t('features.sendEmail.send'), dialog).disabled).toBe(true)
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('shows the recipients and the plain-text hint', async () => {
    const { dialog } = await renderDialog({ target: 'the 3 filtered users' })

    expect(dialog.textContent).toContain(
      t('features.sendEmail.target', { target: 'the 3 filtered users' }),
    )
    expect(dialog.textContent).toContain(t('features.sendEmail.bodyHint'))
  })

  it('requires a purpose, a subject and a body before asking for confirmation', async () => {
    const { wrapper, dialog } = await renderDialog()

    await click(findButton(t('features.sendEmail.send'), dialog))
    expect(dialog.textContent).toContain(t('features.sendEmail.form.problems.purposeRequired'))
    expect(dialog.textContent).toContain(t('features.sendEmail.form.problems.subjectRequired'))
    expect(dialog.textContent).toContain(t('features.sendEmail.form.problems.bodyRequired'))

    await typeInto('#send-email-subject', '   ')
    await typeInto('#send-email-body', 'Body')
    await click(findButton(t('features.sendEmail.send'), dialog))
    expect(dialog.textContent).toContain(t('features.sendEmail.form.problems.subjectRequired'))
    expect(document.body.querySelector('.el-message-box')).toBeNull()
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('emits the composed email with attachments after confirmation', async () => {
    const { wrapper, dialog } = await renderDialog()

    await choosePurpose(dialog, 'Informational')
    await typeInto('#send-email-subject', ' Welcome ')
    await typeInto('#send-email-body', 'Hello\nthere')
    const picked = files(2)
    await pickFiles(fileInput(dialog), picked)
    await click(findButton(t('features.sendEmail.send'), dialog))

    expect(messageBox().textContent).toContain(t('features.sendEmail.confirm.header'))
    expect(messageBox().textContent).toContain(
      t('features.sendEmail.confirm.message', { target: 'Ada Lovelace' }),
    )
    await acceptMessageBox()

    const emitted = wrapper.emitted('submit')?.[0]?.[0] as SendEmailPayload | undefined
    expect(emitted?.purpose).toBe('Informational')
    expect(emitted?.subject).toBe(' Welcome ')
    expect(emitted?.body).toBe('Hello\nthere')
    expect(emitted?.attachments.map((file) => file.name)).toEqual(['file-0.txt', 'file-1.txt'])
  })

  it('states and confirms how many addresses the email reaches once the audience is known', async () => {
    const { wrapper, dialog } = await renderDialog({ target: 'los 28 usuarios', recipients: 22 })

    expect(dialog.textContent).toContain(tp('features.sendEmail.recipients', 22, { count: 22 }))
    await compose(dialog)
    await click(findButton(t('features.sendEmail.send'), dialog))

    expect(messageBox().textContent).toContain(
      tp('features.sendEmail.confirm.messageCount', 22, { count: 22 }),
    )
    await acceptMessageBox()
    expect(wrapper.emitted('submit')).toHaveLength(1)
  })

  it('blocks sending when nobody in the selection has an email address', async () => {
    const { wrapper, dialog } = await renderDialog({ recipients: 0 })

    expect(dialog.textContent).toContain(tp('features.sendEmail.recipients', 0, { count: 0 }))
    const send = findButton(t('features.sendEmail.send'), dialog)
    expect(send.disabled).toBe(true)
    await typeInto('#send-email-subject', 'Hola')
    await typeInto('#send-email-body', 'Texto')
    dialog.querySelector('form')?.dispatchEvent(new Event('submit'))
    await flushPromises()

    expect(document.body.querySelector('.el-message-box')).toBeNull()
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('does not emit when the confirmation is cancelled', async () => {
    const { wrapper, dialog } = await renderDialog()

    await compose(dialog)
    await click(findButton(t('features.sendEmail.send'), dialog))
    await cancelMessageBox()

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('lists, summarizes and removes attachments', async () => {
    const { dialog } = await renderDialog()
    const clickSpy = vi
      .spyOn(HTMLInputElement.prototype, 'click')
      .mockImplementation(() => undefined)

    await click(findButton(t('features.sendEmail.attachments.add'), dialog))
    expect(clickSpy).toHaveBeenCalledTimes(1)

    await pickFiles(fileInput(dialog), [new File(['abc'], 'notes.txt')])
    await pickFiles(fileInput(dialog), [new File(['12345'], 'data.csv')])
    expect(dialog.textContent).toContain(
      tp('features.sendEmail.attachments.summary', 2, { count: 2, size: formatFileSize(8) }),
    )
    expect(
      Array.from(dialog.querySelectorAll('.attachments__name')).map((n) => n.textContent),
    ).toEqual(['notes.txt', 'data.csv'])

    await click(findButtons(t('features.sendEmail.attachments.remove'), dialog)[0] ?? dialog)
    expect(
      Array.from(dialog.querySelectorAll('.attachments__name')).map((n) => n.textContent),
    ).toEqual(['data.csv'])
    expect(dialog.textContent).toContain(
      tp('features.sendEmail.attachments.summary', 1, { count: 1, size: formatFileSize(5) }),
    )

    await pickFiles(fileInput(dialog), [])
    expect(dialog.querySelectorAll('.attachments__name')).toHaveLength(1)

    Object.defineProperty(fileInput(dialog), 'files', { configurable: true, value: null })
    fileInput(dialog).dispatchEvent(new Event('change'))
    await flushPromises()
    expect(dialog.querySelectorAll('.attachments__name')).toHaveLength(1)
  })

  it('rejects more attachments than allowed', async () => {
    const { dialog } = await renderDialog()

    await pickFiles(fileInput(dialog), files(MAX_ATTACHMENTS + 1))

    expect(dialog.textContent).toContain(
      t('features.sendEmail.attachments.tooMany', { max: MAX_ATTACHMENTS }),
    )
    expect(dialog.querySelectorAll('.attachments__name')).toHaveLength(0)

    await pickFiles(fileInput(dialog), files(MAX_ATTACHMENTS))
    expect(dialog.querySelectorAll('.attachments__name')).toHaveLength(MAX_ATTACHMENTS)
    expect(findButton(t('features.sendEmail.attachments.add'), dialog).disabled).toBe(true)
  })

  it('rejects attachments above the total size limit', async () => {
    const { dialog } = await renderDialog()

    await pickFiles(fileInput(dialog), [
      new File([new Uint8Array(MAX_ATTACHMENTS_BYTES)], 'big.bin'),
      new File(['x'], 'extra.txt'),
    ])

    expect(dialog.textContent).toContain(
      t('features.sendEmail.attachments.tooLarge', { max: formatFileSize(MAX_ATTACHMENTS_BYTES) }),
    )
    expect(dialog.querySelectorAll('.attachments__name')).toHaveLength(0)
  })

  it('clears the form each time it opens', async () => {
    const { wrapper, dialog } = await renderDialog()
    await choosePurpose(dialog, 'Promotional')
    await typeInto('#send-email-subject', 'Draft')
    await pickFiles(fileInput(dialog), files(1))

    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(inputValue('#send-email-subject')).toBe('')
    expect(openDialog(t(TITLE)).querySelector('input[type="radio"]:checked')).toBeNull()
    expect(openDialog(t(TITLE)).querySelectorAll('.attachments__name')).toHaveLength(0)
  })

  it('closes on cancel unless an email is being sent', async () => {
    const idle = await renderDialog()
    await click(findButton(t('common.cancel'), idle.dialog))
    expect(idle.wrapper.emitted('update:visible')).toEqual([[false]])
    idle.wrapper.unmount()

    const busy = await renderDialog({ sending: true })
    busy.wrapper.findComponent(ElDialog).vm.$emit('update:modelValue', false)
    await click(findButton(t('features.sendEmail.send'), busy.dialog))
    await typeInto('#send-email-subject', 'Hi')
    await busy.wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(busy.wrapper.emitted('update:visible')).toBeUndefined()
    expect(busy.dialog.textContent).not.toContain(
      t('features.sendEmail.form.problems.subjectRequired'),
    )
    expect(document.body.querySelector('.el-message-box')).toBeNull()
  })
})
