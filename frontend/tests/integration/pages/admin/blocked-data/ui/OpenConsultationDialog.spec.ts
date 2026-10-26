import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { describe, expect, it } from 'vitest'
import { h } from 'vue'

import OpenConsultationDialog from '@/pages/admin/blocked-data/ui/OpenConsultationDialog.vue'

import { renderWithProviders, t } from '../../../../../support/render'
import { click, findButton, openDialog, typeInto } from '../../../../../support/dom'

async function renderDialog(
  props: { codeStep?: boolean; usesAuthenticator?: boolean; error?: string } = {},
) {
  const emitted = { continue: [] as unknown[], resend: 0, submit: [] as unknown[] }
  const rendered = await renderWithProviders(
    {
      render: () =>
        h(OpenConsultationDialog, {
          visible: true,
          codeStep: props.codeStep ?? false,
          usesAuthenticator: props.usesAuthenticator ?? false,
          email: 'dpd@example.test',
          sendingCode: false,
          saving: false,
          error: props.error ?? '',
          onContinue: (request: unknown) => emitted.continue.push(request),
          onResend: () => emitted.resend++,
          onSubmit: (code: unknown) => emitted.submit.push(code),
        }),
    },
    { attach: true },
  )
  await flushPromises()
  return { ...rendered, emitted }
}

describe('OpenConsultationDialog', () => {
  it('asks for the request and the password before emailing the code', async () => {
    const { emitted } = await renderDialog()
    const dialog = openDialog(t('pages.admin.blockedData.open.title'))

    await click(findButton(t('pages.admin.blockedData.open.sendCode'), document.body))

    expect(emitted.continue).toEqual([])
    expect(dialog.textContent).toContain(
      t('pages.admin.blockedData.open.form.problems.authorityRequired'),
    )
    expect(dialog.textContent).toContain(
      t('pages.admin.blockedData.open.form.problems.passwordRequired'),
    )
  })

  it('continues with the request it read', async () => {
    const { wrapper, emitted } = await renderDialog({ usesAuthenticator: true })

    wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', 'Prosecutor')
    await typeInto('#blocked-reference', ' Diligencias 9/2026 ')
    await typeInto('#blocked-password', 'secret')
    await click(findButton(t('pages.admin.blockedData.open.continue'), document.body))

    expect(emitted.continue).toEqual([
      { authority: 'Prosecutor', reference: 'Diligencias 9/2026', currentPassword: 'secret' },
    ])
  })

  it('tells where the code was emailed and lets it be sent again', async () => {
    const { emitted } = await renderDialog({ codeStep: true })
    const dialog = openDialog(t('pages.admin.blockedData.open.title'))

    expect(dialog.textContent).toContain(
      t('pages.admin.blockedData.open.codeSent', { email: 'dpd@example.test' }),
    )
    await click(findButton(t('pages.admin.blockedData.open.resend'), document.body))

    expect(emitted.resend).toBe(1)
  })

  it('asks the authenticator code without offering to email it', async () => {
    await renderDialog({ codeStep: true, usesAuthenticator: true })
    const dialog = openDialog(t('pages.admin.blockedData.open.title'))

    expect(dialog.textContent).toContain(t('pages.admin.blockedData.open.authenticatorIntro'))
    expect(dialog.textContent).not.toContain(t('pages.admin.blockedData.open.resend'))
  })

  it('submits the six digits of the code', async () => {
    const { emitted } = await renderDialog({ codeStep: true })

    await click(findButton(t('pages.admin.blockedData.open.confirm'), document.body))
    expect(emitted.submit).toEqual([])
    expect(document.querySelector('#blocked-code-error')?.textContent).toBe(
      t('pages.admin.blockedData.open.form.problems.codeInvalid'),
    )

    await typeInto('#blocked-code', '654 321')
    await click(findButton(t('pages.admin.blockedData.open.confirm'), document.body))

    expect(emitted.submit).toEqual(['654321'])
  })

  it('shows the rejection the API reported', async () => {
    await renderDialog({ codeStep: true, error: 'El código no es válido.' })

    expect(document.querySelector('[role="alert"]')?.textContent).toBe('El código no es válido.')
  })
})
