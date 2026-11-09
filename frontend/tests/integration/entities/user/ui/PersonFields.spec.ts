import { describe, expect, it } from 'vitest'

import { PersonFields } from '@/entities/user'

import { renderWithProviders, t } from '../../../../support/render'

async function renderFields(props: Record<string, unknown> = {}) {
  const rendered = await renderWithProviders(PersonFields, {
    props: {
      idPrefix: 'p',
      firstName: '',
      lastName: '',
      gender: null,
      errors: {},
      ...props,
    },
  })
  return rendered
}

function labels(wrapper: Awaited<ReturnType<typeof renderFields>>['wrapper']): string[] {
  return wrapper.findAll('.ca-field__label').map((label) => label.text())
}

describe('PersonFields', () => {
  it('asks a person for names, DNI/NIE and gender, letting the browser fill them', async () => {
    const { wrapper } = await renderFields({ nationalId: '', showNationalIdErrors: false })

    expect(labels(wrapper)).toEqual([
      t('common.firstName'),
      t('common.lastName'),
      t('common.nationalId'),
      t('common.gender'),
    ])
    expect(wrapper.get('#p-firstname').attributes('autocomplete')).toBe('given-name')
    expect(wrapper.get('#p-lastname').attributes('autocomplete')).toBe('family-name')
    expect(wrapper.get('#p-national-id').attributes('required')).toBeDefined()
    expect(wrapper.find('#p-dob').exists()).toBe(false)
  })

  it('asks a dependent for a birth date within the range instead of a DNI/NIE', async () => {
    const { wrapper } = await renderFields({
      idPrefix: 'minor-0',
      birthDate: '',
      birthDateRange: { min: '2008-09-18', max: '2026-09-17' },
    })

    expect(labels(wrapper)).toEqual([
      t('common.firstName'),
      t('common.lastName'),
      t('common.birthDate'),
      t('common.gender'),
    ])
    expect(wrapper.get('#minor-0-firstname').attributes('autocomplete')).toBe('off')
    expect(wrapper.get('#minor-0-dob').attributes()).toMatchObject({
      type: 'date',
      min: '2008-09-18',
      max: '2026-09-17',
    })
    expect(wrapper.find('#minor-0-national-id').exists()).toBe(false)
  })

  it('links each reported problem to its field', async () => {
    const { wrapper } = await renderFields({
      birthDate: '',
      birthDateRange: { max: '2026-09-17' },
      errors: { firstName: 'Missing name', birthDate: 'Too old', gender: 'Pick one' },
    })

    expect(wrapper.get('#p-firstname').attributes('aria-describedby')).toBe('p-firstname-error')
    expect(wrapper.get('#p-firstname-error').text()).toBe('Missing name')
    expect(wrapper.get('#p-lastname').attributes('aria-invalid')).toBe('false')
    const birthDate = wrapper.get('#p-dob')
    expect(birthDate.classes()).toContain('person-fields__date--invalid')
    expect(birthDate.attributes('aria-invalid')).toBe('true')
    expect(birthDate.attributes('aria-describedby')).toBe('p-dob-error')
    expect(birthDate.attributes('min')).toBeUndefined()
    expect(wrapper.get('#p-gender-error').text()).toBe('Pick one')
    expect(wrapper.get('[data-testid="person-fields-gender"]').classes()).toContain('ca-invalid')
  })

  it('reports what the person types and picks', async () => {
    const { wrapper } = await renderFields({ nationalId: '', birthDate: '' })

    await wrapper.get('#p-firstname').setValue('Ada')
    await wrapper.get('#p-lastname').setValue('Lovelace')
    await wrapper.get('#p-national-id').setValue('12345678Z')
    await wrapper.get('#p-dob').setValue('2016-02-03')
    wrapper.findComponent({ name: 'ElSelect' }).vm.$emit('update:modelValue', 'Female')

    expect(wrapper.emitted('update:firstName')).toEqual([['Ada']])
    expect(wrapper.emitted('update:lastName')).toEqual([['Lovelace']])
    expect(wrapper.emitted('update:nationalId')).toEqual([['12345678Z']])
    expect(wrapper.emitted('update:birthDate')).toEqual([['2016-02-03']])
    expect(wrapper.emitted('update:gender')).toEqual([['Female']])
  })
})
