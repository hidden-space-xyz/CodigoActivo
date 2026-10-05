import { flushPromises } from '@vue/test-utils'
import { ElDatePicker, ElInputNumber } from 'element-plus'
import { describe, expect, it } from 'vitest'

import type { Partner } from '@/entities/partner'
import PartnerFormDialog from '@/pages/admin/partners/ui/PartnerFormDialog.vue'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../../support/server'
import { buildPartner } from '../../../../../support/models'
import {
  click,
  findButton,
  inputValue,
  openDialog,
  pickFiles,
  propOf,
  typeInto,
} from '../../../../../support/dom'

async function renderDialog(partner: Partner | null = null) {
  server.use(
    http.get('/api/files/:id', () => HttpResponse.json({ name: 'logo.png', extension: 'png' })),
  )
  const rendered = await renderWithProviders(PartnerFormDialog, {
    props: { visible: false, partner, saving: false },
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

  it('populates the form from the partner being edited', async () => {
    await renderDialog(buildPartner())

    openDialog(t('pages.admin.partners.form.editHeader'))
    expect(inputValue('#partner-name')).toBe('Acme')
    expect(inputValue('#partner-website')).toBe('https://acme.test')
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

  it('submits tier zero when the tier input is cleared', async () => {
    const { wrapper } = await renderDialog(buildPartner({ tier: 4 }))
    const dialog = openDialog(t('pages.admin.partners.form.editHeader'))

    wrapper.findComponent(ElInputNumber).vm.$emit('update:modelValue', undefined)
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ tier: 0, name: 'Acme' })
  })

  it('shows the upload error and does not submit when the logo upload fails', async () => {
    server.use(http.post('/api/files', () => apiError(500)))
    const { wrapper } = await renderDialog()
    const dialog = openDialog(t('pages.admin.partners.form.newHeader'))

    await typeInto('#partner-name', 'Initech')
    wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', new Date(2025, 0, 2))
    await pickFiles(fileInput(dialog), [new File(['x'], 'logo.png', { type: 'image/png' })])
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('entities.file.thumbnail.uploadFailed'))
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
