import { flushPromises } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { describe, expect, it } from 'vitest'

import EventRatingDialog from '@/features/account/ui/EventRatingDialog.vue'

import {
  buttonByText,
  buttonsByText,
  click,
  fill,
  openDialogs,
} from '../../../../support/fixtures/account/dom'
import { renderWithProviders, t } from '../../../../support/render'

async function renderDialog(props: Partial<Record<string, unknown>> = {}) {
  const { wrapper } = await renderWithProviders(EventRatingDialog, {
    attach: true,
    props: { visible: true, eventTitle: 'Hackathon', saving: false, ...props },
  })
  return wrapper
}

function field(id: string): HTMLTextAreaElement {
  const element = document.querySelector<HTMLTextAreaElement>(`#${id}`)
  if (!element) throw new Error(`Missing #${id}`)
  return element
}

describe('EventRatingDialog', () => {
  it('shows the event title and the anonymity notice, and always opens with an empty form', async () => {
    const wrapper = await renderDialog()

    const [dialog] = openDialogs()
    expect(dialog?.textContent).toContain(t('features.account.history.dialog.header'))
    expect(dialog?.textContent).toContain('Hackathon')
    expect(dialog?.textContent).toContain(t('features.account.history.dialog.anonymousNotice'))
    expect(field('rating-most').value).toBe('')
    expect(field('rating-least').value).toBe('')
    expect(field('rating-suggestions').value).toBe('')
    expect(
      buttonsByText(document.body, t('features.account.history.dialog.clearScore')),
    ).toHaveLength(0)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([
      [{ score: 0, mostLiked: '', leastLiked: '', suggestions: '' }],
    ])
  })

  it('lets the user set and clear the score and fill free-text answers before submitting', async () => {
    const wrapper = await renderDialog()
    const clearLabel = t('features.account.history.dialog.clearScore')

    const stars = document.querySelectorAll('.el-rate__item')
    expect(stars).toHaveLength(5)
    await click(stars[2] as Element)
    await click(buttonByText(document.body, clearLabel))
    await click(stars[1] as Element)
    expect(buttonsByText(document.body, clearLabel)).toHaveLength(1)

    await fill(document, '#rating-most', 'Todo')
    await fill(document, '#rating-least', 'El calor')
    await fill(document, '#rating-suggestions', 'Repetir')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([
      [{ score: 2, mostLiked: 'Todo', leastLiked: 'El calor', suggestions: 'Repetir' }],
    ])
  })

  it('emits close from the cancel button and from the dialog close control', async () => {
    const wrapper = await renderDialog()

    await click(buttonByText(document.body, t('common.cancel')))
    const dialog = wrapper.findComponent(ElDialog)
    dialog.vm.$emit('update:modelValue', true)
    dialog.vm.$emit('update:modelValue', false)

    expect(wrapper.emitted('close')).toHaveLength(2)
  })

  it('discards unsaved answers each time the dialog is reopened', async () => {
    const wrapper = await renderDialog()

    await fill(document, '#rating-most', 'Cambio sin guardar')
    await wrapper.setProps({ visible: false })
    await flushPromises()
    await fill(document, '#rating-least', 'Otro cambio')
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(field('rating-most').value).toBe('')
    expect(field('rating-least').value).toBe('')
  })

  it('shows a busy save button while the rating is stored', async () => {
    await renderDialog({ saving: true })

    const save = buttonByText(document.body, t('common.save'))
    expect(save.getAttribute('aria-busy')).toBe('true')
    expect(save.disabled).toBe(true)
  })
})
