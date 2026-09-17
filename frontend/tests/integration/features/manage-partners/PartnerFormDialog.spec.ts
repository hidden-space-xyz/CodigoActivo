import { flushPromises } from '@vue/test-utils'
import { ElDatePicker, ElInputNumber } from 'element-plus'
import { describe, expect, it } from 'vitest'

import { PartnerFormDialog } from '@/features/manage-partners'
import type { PartnerResponse } from '@/shared/api/generated/models'

import { buildPartner } from '../../../support/fixtures/admin-content/builders'
import {
  click,
  findButton,
  inputValue,
  openDialog,
  pickFiles,
  typeInto,
} from '../../../support/fixtures/admin-content/helpers'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

async function renderDialog(partner: PartnerResponse | null = null) {
  server.use(
    http.get('/api/files/:id', () => HttpResponse.json({ name: 'logo', extension: '.png' })),
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
    const dialog = openDialog(t('features.managePartners.newHeader'))

    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('features.managePartners.nameRequired'))
    expect(dialog.textContent).toContain(t('features.managePartners.fromDateRequired'))
    expect(dialog.textContent).toContain(t('common.imageRequired'))
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('populates the form from the partner being edited', async () => {
    await renderDialog(buildPartner())

    openDialog(t('features.managePartners.editHeader'))
    expect(inputValue('#partner-name')).toBe('Acme')
    expect(inputValue('#partner-website')).toBe('https://acme.test')
  })

  it('emits a cleared visibility when cancelled', async () => {
    const { wrapper } = await renderDialog()

    await click(findButton(t('common.cancel'), openDialog(t('features.managePartners.newHeader'))))

    expect(wrapper.emitted('update:visible')).toEqual([[false]])
  })

  it('submits tier zero when the tier input is cleared', async () => {
    const { wrapper } = await renderDialog(buildPartner({ tier: 4 }))
    const dialog = openDialog(t('features.managePartners.editHeader'))

    wrapper.findComponent(ElInputNumber).vm.$emit('update:modelValue', undefined)
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ tier: 0, name: 'Acme' })
  })

  it('shows the upload error and does not submit when the logo upload fails', async () => {
    server.use(http.post('/api/files', () => apiError(500)))
    const { wrapper } = await renderDialog()
    const dialog = openDialog(t('features.managePartners.newHeader'))

    await typeInto('#partner-name', 'Initech')
    wrapper.findComponent(ElDatePicker).vm.$emit('update:modelValue', new Date(2025, 0, 2))
    await pickFiles(fileInput(dialog), [new File(['x'], 'logo.png', { type: 'image/png' })])
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('entities.file.thumbnail.uploadFailed'))
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('resets validation state when reopened', async () => {
    const { wrapper } = await renderDialog()
    const dialog = openDialog(t('features.managePartners.newHeader'))
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('features.managePartners.nameRequired'))

    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(openDialog(t('features.managePartners.newHeader')).textContent).not.toContain(
      t('features.managePartners.nameRequired'),
    )
  })
})
