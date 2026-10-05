import { flushPromises } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { describe, expect, it } from 'vitest'

import EventRatingDialog from '@/pages/account/ui/EventRatingDialog.vue'

import { renderWithProviders, t } from '../../../../support/render'
import { click, findButton, findButtons, openDialogs, typeInto } from '../../../../support/dom'

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
  it('shows the event title and the anonymity notice, and opens with an empty form it does not send', async () => {
    const wrapper = await renderDialog()

    const [dialog] = openDialogs()
    expect(dialog?.textContent).toContain(t('pages.account.history.dialog.header'))
    expect(dialog?.textContent).toContain('Hackathon')
    expect(dialog?.textContent).toContain(t('pages.account.history.dialog.anonymousNotice'))
    expect(field('rating-most').value).toBe('')
    expect(field('rating-least').value).toBe('')
    expect(field('rating-suggestions').value).toBe('')
    expect(findButtons(t('pages.account.history.dialog.clearScore'), document.body)).toHaveLength(0)
    expect(dialog?.textContent).toContain(t('pages.account.history.dialog.emptyHint'))
    expect(findButton(t('pages.account.history.dialog.submit'), document.body).disabled).toBe(true)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('sends written answers without a score', async () => {
    const wrapper = await renderDialog()

    await typeInto('#rating-suggestions', '   ', document)
    expect(findButton(t('pages.account.history.dialog.submit'), document.body).disabled).toBe(true)
    await typeInto('#rating-suggestions', 'Más talleres', document)
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([
      [{ score: null, mostLiked: '', leastLiked: '', suggestions: 'Más talleres' }],
    ])
    expect(openDialogs()[0]?.textContent).not.toContain(t('pages.account.history.dialog.emptyHint'))
  })

  it('lets the user set and clear the score and fill free-text answers before submitting', async () => {
    const wrapper = await renderDialog()
    const clearLabel = t('pages.account.history.dialog.clearScore')

    const stars = document.querySelectorAll('.el-rate__item')
    expect(stars).toHaveLength(5)
    await click(stars[2] as Element)
    await click(findButton(clearLabel, document.body))
    await click(stars[1] as Element)
    expect(findButtons(clearLabel, document.body)).toHaveLength(1)

    await typeInto('#rating-most', 'Todo', document)
    await typeInto('#rating-least', 'El calor', document)
    await typeInto('#rating-suggestions', 'Repetir', document)
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toEqual([
      [{ score: 2, mostLiked: 'Todo', leastLiked: 'El calor', suggestions: 'Repetir' }],
    ])
  })

  it('emits close from the cancel button and from the dialog close control', async () => {
    const wrapper = await renderDialog()

    await click(findButton(t('common.cancel'), document.body))
    const dialog = wrapper.findComponent(ElDialog)
    dialog.vm.$emit('update:modelValue', true)
    dialog.vm.$emit('update:modelValue', false)

    expect(wrapper.emitted('close')).toHaveLength(2)
  })

  it('discards unsaved answers each time the dialog is reopened', async () => {
    const wrapper = await renderDialog()

    await typeInto('#rating-most', 'Cambio sin guardar', document)
    await wrapper.setProps({ visible: false })
    await flushPromises()
    await typeInto('#rating-least', 'Otro cambio', document)
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(field('rating-most').value).toBe('')
    expect(field('rating-least').value).toBe('')
  })

  it('shows a busy save button while the rating is stored', async () => {
    await renderDialog({ saving: true })

    await typeInto('#rating-most', 'Todo', document)
    const save = findButton(t('pages.account.history.dialog.submit'), document.body)
    expect(save.getAttribute('aria-busy')).toBe('true')
    expect(save.disabled).toBe(true)
  })
})
