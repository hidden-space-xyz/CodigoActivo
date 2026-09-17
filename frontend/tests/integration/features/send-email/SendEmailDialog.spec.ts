import { flushPromises } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { SendEmailDialog } from '@/features/send-email'
import {
  MAX_ATTACHMENTS,
  MAX_ATTACHMENTS_BYTES,
  type SendEmailPayload,
} from '@/features/send-email/model/useSendEmail'
import { formatFileSize } from '@/shared/lib'

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
} from '../../../support/fixtures/admin-content/helpers'
import { renderWithProviders, t } from '../../../support/render'

const TITLE = 'features.sendEmail.header'

async function renderDialog(props: { target?: string; sending?: boolean } = {}) {
  const rendered = await renderWithProviders(SendEmailDialog, {
    props: {
      visible: false,
      target: props.target ?? 'Ada Lovelace',
      sending: props.sending ?? false,
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

function files(count: number, size = 10): File[] {
  return Array.from(
    { length: count },
    (_, index) => new File(['x'.repeat(size)], `file-${index}.txt`),
  )
}

describe('SendEmailDialog', () => {
  it('shows the recipients and the plain-text hint', async () => {
    const { dialog } = await renderDialog({ target: 'the 3 filtered users' })

    expect(dialog.textContent).toContain(
      t('features.sendEmail.target', { target: 'the 3 filtered users' }),
    )
    expect(dialog.textContent).toContain(t('features.sendEmail.bodyHint'))
  })

  it('requires a subject and a body before asking for confirmation', async () => {
    const { wrapper, dialog } = await renderDialog()

    await click(findButton(t('features.sendEmail.send'), dialog))
    expect(dialog.textContent).toContain(t('features.sendEmail.subjectRequired'))
    expect(dialog.textContent).toContain(t('features.sendEmail.bodyRequired'))

    await typeInto('#send-email-subject', '   ')
    await typeInto('#send-email-body', 'Body')
    await click(findButton(t('features.sendEmail.send'), dialog))
    expect(dialog.textContent).toContain(t('features.sendEmail.subjectRequired'))
    expect(document.body.querySelector('.el-message-box')).toBeNull()
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('emits the composed email with attachments after confirmation', async () => {
    const { wrapper, dialog } = await renderDialog()

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
    expect(emitted?.subject).toBe(' Welcome ')
    expect(emitted?.body).toBe('Hello\nthere')
    expect(emitted?.attachments.map((file) => file.name)).toEqual(['file-0.txt', 'file-1.txt'])
  })

  it('does not emit when the confirmation is cancelled', async () => {
    const { wrapper, dialog } = await renderDialog()

    await typeInto('#send-email-subject', 'Hi')
    await typeInto('#send-email-body', 'Body')
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
    await typeInto('#send-email-subject', 'Draft')
    await pickFiles(fileInput(dialog), files(1))

    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(inputValue('#send-email-subject')).toBe('')
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
    expect(busy.dialog.textContent).not.toContain(t('features.sendEmail.subjectRequired'))
    expect(document.body.querySelector('.el-message-box')).toBeNull()
  })
})
