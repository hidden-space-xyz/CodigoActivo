import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { NationalIdInput } from '@/shared/ui'

import { renderWithProviders, t } from '../../../support/render'

async function renderInput(props: Record<string, unknown> = {}) {
  const rendered = await renderWithProviders(NationalIdInput, {
    props: {
      modelValue: '',
      id: 'dni',
      showErrors: false,
      'onUpdate:modelValue': (value: string) => rendered.wrapper.setProps({ modelValue: value }),
      ...props,
    },
  })
  const input = rendered.wrapper.get('#dni')
  const message = () => rendered.wrapper.find('.national-id-input__error')
  const invalid = () => rendered.wrapper.find('.ca-invalid').exists()
  return { ...rendered, input, message, invalid }
}

describe('NationalIdInput', () => {
  it('renders a required uppercase input sized for a DNI or NIE', async () => {
    const { input } = await renderInput()

    expect(input.element.tagName).toBe('INPUT')
    expect(input.attributes()).toMatchObject({
      autocomplete: 'off',
      autocapitalize: 'characters',
      spellcheck: 'false',
      maxlength: '12',
    })
    expect(input.attributes('required')).toBeDefined()
  })

  it('waits until the input is left before reporting a malformed value', async () => {
    const { input, message, invalid } = await renderInput()

    await input.setValue('1234567')
    expect(message().exists()).toBe(false)
    expect(invalid()).toBe(false)

    await input.trigger('blur')

    expect(message().text()).toBe(t('validation.nationalIdFormat'))
    expect(invalid()).toBe(true)
  })

  it('reports a control letter that does not match and clears it once fixed', async () => {
    const { input, message, invalid } = await renderInput()

    await input.setValue('12345678A')
    await input.trigger('blur')
    expect(message().text()).toBe(t('validation.nationalIdLetter'))

    await input.setValue('12345678Z')

    expect(message().exists()).toBe(false)
    expect(invalid()).toBe(false)
  })

  it('normalizes the value when the input is left', async () => {
    const { wrapper, input, message } = await renderInput()

    await input.setValue(' x-1234 567-l ')
    await input.trigger('blur')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual(['X1234567L'])
    expect((input.element as HTMLInputElement).value).toBe('X1234567L')
    expect(message().exists()).toBe(false)
  })

  it('normalizes the latest value even when the input is left in the tick it changed', async () => {
    const { wrapper, input } = await renderInput()
    const element = input.element as HTMLInputElement

    element.value = 'y 1234567-x'
    element.dispatchEvent(new Event('input'))
    element.dispatchEvent(new FocusEvent('blur'))
    await flushPromises()

    expect(wrapper.emitted('update:modelValue')).toEqual([['y 1234567-x'], ['Y1234567X']])
    expect(element.value).toBe('Y1234567X')
  })

  it('shows a value replaced by the form, such as when a dialog is reopened', async () => {
    const { wrapper, input } = await renderInput({ modelValue: '12345678Z' })
    expect((input.element as HTMLInputElement).value).toBe('12345678Z')

    await wrapper.setProps({ modelValue: 'X1234567L' })

    expect((input.element as HTMLInputElement).value).toBe('X1234567L')
  })

  it('emits nothing more on blur when the value is already normalized', async () => {
    const { wrapper, input } = await renderInput()

    await input.setValue('12345678Z')
    await input.trigger('blur')

    expect(wrapper.emitted('update:modelValue')).toEqual([['12345678Z']])
  })

  it('leaves a blank input alone until the form asks for every error', async () => {
    const { wrapper, input, message } = await renderInput()

    await input.trigger('blur')
    expect(message().exists()).toBe(false)

    await wrapper.setProps({ showErrors: true })

    expect(message().text()).toBe(t('validation.nationalIdFormat'))
  })
})
