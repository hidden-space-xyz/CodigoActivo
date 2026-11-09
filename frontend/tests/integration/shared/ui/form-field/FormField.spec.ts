import { h } from 'vue'
import { describe, expect, it } from 'vitest'

import { FormField } from '@/shared/ui/form-field'

import { renderWithProviders } from '../../../../support/render'

async function renderField(props: Record<string, unknown>) {
  const { wrapper } = await renderWithProviders(FormField, {
    props: { label: 'Name', ...props },
    slots: {
      default: (slot: { control: Record<string, unknown>; invalid: boolean }) =>
        h('input', { ...slot.control, 'data-invalid': String(slot.invalid) }),
    },
  })
  return { wrapper, input: wrapper.find('input') }
}

describe('FormField', () => {
  it('names the control with its label and leaves it valid without a problem', async () => {
    const { wrapper, input } = await renderField({ for: 'partner-name', hint: 'As printed' })

    expect(wrapper.classes()).toEqual(['ca-field'])
    expect(wrapper.get('label').attributes('for')).toBe('partner-name')
    expect(wrapper.get('label').text()).toBe('Name')
    expect(input.attributes('id')).toBe('partner-name')
    expect(input.classes()).not.toContain('ca-invalid')
    expect(input.attributes('aria-invalid')).toBe('false')
    expect(input.attributes('aria-describedby')).toBeUndefined()
    expect(input.attributes('data-invalid')).toBe('false')
    expect(wrapper.get('.ca-field__hint').text()).toBe('As printed')
    expect(wrapper.find('.ca-field__error').exists()).toBe(false)
  })

  it('shows the message in place of the hint and links it to the control', async () => {
    const { wrapper, input } = await renderField({
      for: 'partner-name',
      hint: 'As printed',
      error: 'Required',
      errorTestid: 'partner-form-name-error',
      wide: true,
    })

    const error = wrapper.get('.ca-field__error')
    expect(wrapper.classes()).toContain('ca-field--wide')
    expect(error.text()).toBe('Required')
    expect(error.attributes('id')).toBe('partner-name-error')
    expect(error.attributes('data-testid')).toBe('partner-form-name-error')
    expect(input.classes()).toContain('ca-invalid')
    expect(input.attributes('aria-invalid')).toBe('true')
    expect(input.attributes('aria-describedby')).toBe('partner-name-error')
    expect(input.attributes('data-invalid')).toBe('true')
    expect(wrapper.find('.ca-field__hint').exists()).toBe(false)
  })

  it('marks the control refused without a message to link', async () => {
    const { wrapper, input } = await renderField({ for: 'event-title', invalid: true })

    expect(input.classes()).toContain('ca-invalid')
    expect(input.attributes('aria-invalid')).toBe('true')
    expect(input.attributes('aria-describedby')).toBeUndefined()
    expect(wrapper.find('.ca-field__error').exists()).toBe(false)
  })

  it('titles a control that a label cannot name and leaves its message without an id', async () => {
    const { wrapper, input } = await renderField({ error: 'Too long' })

    expect(wrapper.find('label').exists()).toBe(false)
    expect(wrapper.get('div.ca-field__label').text()).toBe('Name')
    expect(input.attributes('id')).toBeUndefined()
    expect(wrapper.get('.ca-field__error').attributes('id')).toBeUndefined()
    expect(wrapper.get('.ca-field__error').attributes('data-testid')).toBeUndefined()
  })
})
