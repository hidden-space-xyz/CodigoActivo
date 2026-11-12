import { ElColorPicker } from 'element-plus'
import { describe, expect, it } from 'vitest'

import { ColorField } from '@/shared/ui/color-field'

import { renderWithProviders } from '../../../../support/render'

async function renderField(color: string) {
  return renderWithProviders(ColorField, {
    props: { modelValue: color, labelId: 'tag-color', label: 'Color', preview: 'Talla M' },
  })
}

describe('ColorField', () => {
  it('previews the text with the color and names the group with its label', async () => {
    const { wrapper } = await renderField('00FF00')

    expect(wrapper.find('#tag-color').text()).toBe('Color')
    expect(wrapper.find('[role="group"]').attributes('aria-labelledby')).toBe('tag-color')
    expect(wrapper.find('.color-field__hex').text()).toBe('#00FF00')
    expect(wrapper.text()).toContain('Talla M')
  })

  it('emits the picked color, falling back to the default one when cleared', async () => {
    const { wrapper } = await renderField('#FF0000')

    wrapper.findComponent(ElColorPicker).vm.$emit('update:modelValue', '#123456')
    wrapper.findComponent(ElColorPicker).vm.$emit('update:modelValue', null)

    expect(wrapper.emitted('update:modelValue')).toEqual([['#123456'], ['#6366F1']])
  })
})
