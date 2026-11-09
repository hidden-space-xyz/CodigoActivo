import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import { describe, expect, it } from 'vitest'

import { FormDialog } from '@/shared/ui/form-dialog'

import { click, findButton, openDialog } from '../../../../support/dom'
import { renderWithProviders, t } from '../../../../support/render'

async function renderDialog(props: Record<string, unknown> = {}) {
  const rendered = await renderWithProviders(FormDialog, {
    props: {
      visible: true,
      title: 'New partner',
      width: 'min(460px, 92vw)',
      saving: false,
      'onUpdate:visible': (value: boolean) => rendered.wrapper.setProps({ visible: value }),
      ...props,
    },
    slots: { default: () => h('input', { id: 'partner-name' }) },
    attach: true,
  })
  const dialog = openDialog('New partner')
  return { ...rendered, dialog }
}

describe('FormDialog', () => {
  it('holds the fields in a form that grows freely', async () => {
    const { dialog } = await renderDialog()

    const form = dialog.querySelector('form')
    expect(form?.querySelector('#partner-name')).not.toBeNull()
    expect(form?.classList.contains('form-dialog--scroll')).toBe(false)
    expect(form?.getAttribute('style')).toBeNull()
    expect(findButton(t('common.save'), dialog).dataset.testid).toBe('form-dialog-save')
  })

  it('scrolls a long form past the height it is given', async () => {
    const { dialog } = await renderDialog({ scrollHeight: '68vh' })

    const form = dialog.querySelector('form')
    expect(form?.classList.contains('form-dialog--scroll')).toBe(true)
    expect(form?.style.getPropertyValue('--form-dialog-height')).toBe('68vh')
  })

  it('submits from the confirm button and from the form itself', async () => {
    const { wrapper, dialog } = await renderDialog({ confirmLabel: 'Create' })

    await click(findButton('Create', dialog))
    dialog.querySelector('form')?.dispatchEvent(new Event('submit', { cancelable: true }))
    await flushPromises()

    expect(wrapper.emitted('submit')).toEqual([[], []])
  })

  it('closes only through cancel, which waits while the parent saves', async () => {
    const { wrapper, dialog } = await renderDialog({ saving: true })

    expect(findButton(t('common.cancel'), dialog).disabled).toBe(true)
    expect(findButton(t('common.save'), dialog).classList).toContain('is-loading')

    await wrapper.setProps({ saving: false })
    await click(findButton(t('common.cancel'), dialog))

    expect(wrapper.emitted('update:visible')).toEqual([[false]])
  })
})
