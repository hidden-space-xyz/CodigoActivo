import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { describe, expect, it } from 'vitest'
import { h } from 'vue'

import OpenConsultationDialog from '@/pages/admin/blocked-data/ui/OpenConsultationDialog.vue'

import { renderWithProviders, t } from '../../../../../support/render'
import { click, findButton, openDialog, typeInto } from '../../../../../support/dom'

async function renderDialog(error = '') {
  const submitted: unknown[] = []
  const rendered = await renderWithProviders(
    {
      render: () =>
        h(OpenConsultationDialog, {
          visible: true,
          saving: false,
          error,
          onSubmit: (input: unknown) => submitted.push(input),
        }),
    },
    { attach: true },
  )
  await flushPromises()
  return { ...rendered, submitted }
}

describe('OpenConsultationDialog', () => {
  it('asks for every field before opening', async () => {
    const { submitted } = await renderDialog()
    const dialog = openDialog(t('pages.admin.blockedData.open.title'))

    await click(findButton(t('pages.admin.blockedData.open.confirm'), document.body))

    expect(submitted).toEqual([])
    expect(dialog.textContent).toContain(
      t('pages.admin.blockedData.open.form.problems.authorityRequired'),
    )
    expect(dialog.textContent).toContain(
      t('pages.admin.blockedData.open.form.problems.codeInvalid'),
    )
  })

  it('submits the request it read', async () => {
    const { wrapper, submitted } = await renderDialog()

    wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', 'Prosecutor')
    await typeInto('#blocked-reference', ' Diligencias 9/2026 ')
    await typeInto('#blocked-password', 'secret')
    await typeInto('#blocked-code', '654 321')
    await click(findButton(t('pages.admin.blockedData.open.confirm'), document.body))

    expect(submitted).toEqual([
      {
        authority: 'Prosecutor',
        reference: 'Diligencias 9/2026',
        currentPassword: 'secret',
        code: '654321',
      },
    ])
  })

  it('shows the rejection the API reported under the code', async () => {
    await renderDialog('El código no es válido.')

    expect(document.querySelector('#blocked-code-error')?.textContent).toBe(
      'El código no es válido.',
    )
  })
})
