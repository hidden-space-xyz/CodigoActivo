import { flushPromises } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { describe, expect, it } from 'vitest'

import type { AccountEventRating } from '@/entities/account'
import EventRatingDialog from '@/features/account/ui/EventRatingDialog.vue'

import {
  buttonByText,
  buttonsByText,
  click,
  fill,
  openDialogs,
} from '../../../../support/fixtures/account/dom'
import { renderWithProviders, t } from '../../../../support/render'

const RATING: AccountEventRating = {
  score: 4,
  mostLiked: 'Los talleres',
  leastLiked: 'El calor',
  suggestions: 'Más agua',
}

async function renderDialog(props: Partial<Record<string, unknown>> = {}) {
  const { wrapper } = await renderWithProviders(EventRatingDialog, {
    attach: true,
    props: { visible: true, eventTitle: 'Hackathon', rating: null, saving: false, ...props },
  })
  return wrapper
}

function field(id: string): HTMLTextAreaElement {
  const element = document.querySelector<HTMLTextAreaElement>(`#${id}`)
  if (!element) throw new Error(`Missing #${id}`)
  return element
}

describe('EventRatingDialog', () => {
  it('shows the event and prefills the form from an existing rating', async () => {
    const wrapper = await renderDialog({ rating: RATING })

    const [dialog] = openDialogs()
    expect(dialog?.textContent).toContain(t('features.account.history.dialog.header'))
    expect(dialog?.textContent).toContain('Hackathon')
    expect(field('rating-most').value).toBe('Los talleres')
    expect(field('rating-least').value).toBe('El calor')
    expect(field('rating-suggestions').value).toBe('Más agua')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([[RATING]])
  })

  it('starts empty without a score and lets the user set and clear stars', async () => {
    const wrapper = await renderDialog()
    const clearLabel = t('features.account.history.dialog.clearScore')

    expect(field('rating-most').value).toBe('')
    expect(buttonsByText(document.body, clearLabel)).toHaveLength(0)

    const stars = document.querySelectorAll('.el-rate__item')
    expect(stars).toHaveLength(5)
    await click(stars[2] as Element)
    await click(buttonByText(document.body, clearLabel))
    await click(stars[1] as Element)
    expect(buttonsByText(document.body, clearLabel)).toHaveLength(1)

    await fill(document, '#rating-most', 'Todo')
    await fill(document, '#rating-suggestions', 'Repetir')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([
      [{ score: 2, mostLiked: 'Todo', leastLiked: '', suggestions: 'Repetir' }],
    ])
  })

  it('removes the score when the user clears it', async () => {
    const wrapper = await renderDialog({ rating: RATING })

    await click(buttonByText(document.body, t('features.account.history.dialog.clearScore')))
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')?.[0]).toEqual([{ ...RATING, score: 0 }])
  })

  it('emits close from the cancel button and from the dialog close control', async () => {
    const wrapper = await renderDialog()

    await click(buttonByText(document.body, t('common.cancel')))
    const dialog = wrapper.findComponent(ElDialog)
    dialog.vm.$emit('update:modelValue', true)
    dialog.vm.$emit('update:modelValue', false)

    expect(wrapper.emitted('close')).toHaveLength(2)
  })

  it('resets unsaved answers each time the dialog opens', async () => {
    const wrapper = await renderDialog({ rating: RATING })

    await fill(document, '#rating-most', 'Cambio sin guardar')
    await wrapper.setProps({ visible: false })
    await flushPromises()
    await fill(document, '#rating-least', 'Otro cambio')
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(field('rating-most').value).toBe('Los talleres')
    expect(field('rating-least').value).toBe('El calor')
  })

  it('shows a busy save button while the rating is stored', async () => {
    await renderDialog({ saving: true })

    const save = buttonByText(document.body, t('common.save'))
    expect(save.getAttribute('aria-busy')).toBe('true')
    expect(save.disabled).toBe(true)
  })
})
