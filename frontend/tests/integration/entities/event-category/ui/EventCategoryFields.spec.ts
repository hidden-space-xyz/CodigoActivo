import { ElColorPicker } from 'element-plus'
import { describe, expect, it } from 'vitest'

import { EventCategoryFields } from '@/entities/event-category'

import { renderWithProviders, t } from '../../../../support/render'

async function renderFields(props: { name: string; color: string; invalid?: boolean }) {
  return renderWithProviders(EventCategoryFields, { props: { invalid: false, ...props } })
}

describe('EventCategoryFields', () => {
  it('previews the category with its name and hex color', async () => {
    const { wrapper } = await renderFields({ name: 'Talk', color: '00FF00' })

    expect(wrapper.find('.category-fields__hex').text()).toBe('#00FF00')
    expect(wrapper.text()).toContain('Talk')
  })

  it('previews an example until a name is typed and marks a refused name', async () => {
    const { wrapper } = await renderFields({ name: ' ', color: '#FF0000', invalid: true })

    expect(wrapper.text()).toContain(t('entities.eventCategory.fields.example'))
    expect(wrapper.find('.ca-invalid').exists()).toBe(true)
  })

  it('emits the picked color, falling back to the default one when cleared', async () => {
    const { wrapper } = await renderFields({ name: 'Talk', color: '#FF0000' })

    wrapper.findComponent(ElColorPicker).vm.$emit('update:modelValue', '#123456')
    wrapper.findComponent(ElColorPicker).vm.$emit('update:modelValue', null)

    expect(wrapper.emitted('update:color')).toEqual([['#123456'], ['#6366F1']])
  })
})
