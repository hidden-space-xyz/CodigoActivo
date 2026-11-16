import { flushPromises } from '@vue/test-utils'
import { ElDatePicker } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import PartnerFormDialog from '@/pages/admin/partners/ui/PartnerFormDialog.vue'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, server } from '../../../../../support/server'
import {
  click,
  findButton,
  openDialog,
  pickFiles,
  propOf,
  typeInto,
} from '../../../../../support/dom'

async function renderDialog() {
  const rendered = await renderWithProviders(PartnerFormDialog, {
    props: { visible: false, partner: null, saving: false },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return rendered
}

function fileInput(dialog: HTMLElement): HTMLInputElement {
  const input = dialog.querySelector<HTMLInputElement>('input[type="file"]')
  if (!input) throw new Error('missing file input')
  return input
}

describe('PartnerFormDialog', () => {
  it('shows required field errors and does not submit an empty form', async () => {
    const { wrapper } = await renderDialog()
    const dialog = openDialog(t('pages.admin.partners.form.newHeader'))

    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('pages.admin.partners.form.problems.nameRequired'))
    expect(dialog.textContent).toContain(t('pages.admin.partners.form.problems.fromDateRequired'))
    expect(dialog.textContent).toContain(t('common.imageRequired'))
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('does not let the start day be picked after today', async () => {
    const { wrapper } = await renderDialog()
    const disabled = propOf(wrapper.findComponent(ElDatePicker), 'disabledDate') as (
      date: Date,
    ) => boolean
    const today = new Date()

    expect(disabled(today)).toBe(false)
    expect(disabled(new Date(today.getFullYear() + 1, 0, 1))).toBe(true)
  })

  it('emits a cleared visibility when cancelled', async () => {
    const { wrapper } = await renderDialog()

    await click(
      findButton(t('common.cancel'), openDialog(t('pages.admin.partners.form.newHeader'))),
    )

    expect(wrapper.emitted('update:visible')).toEqual([[false]])
  })

  it('shows the upload error and does not submit when the logo upload fails', async () => {
    server.use(http.post('/api/files', () => apiError(500)))
    const { wrapper } = await renderDialog()
    const dialog = openDialog(t('pages.admin.partners.form.newHeader'))

    await typeInto('#partner-name', 'Initech')
    wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', new Date(2025, 0, 2))
    await pickFiles(fileInput(dialog), [new File(['x'], 'logo.png', { type: 'image/png' })])
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(dialog.textContent).toContain(t('common.imageUploadFailed')))
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('resets validation state when reopened', async () => {
    const { wrapper } = await renderDialog()
    const dialog = openDialog(t('pages.admin.partners.form.newHeader'))
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('pages.admin.partners.form.problems.nameRequired'))

    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(openDialog(t('pages.admin.partners.form.newHeader')).textContent).not.toContain(
      t('pages.admin.partners.form.problems.nameRequired'),
    )
  })
})
