import { describe, expect, it } from 'vitest'

import { useForm, type FormReading } from '@/shared/lib/form'

import { mountComposable, t } from '../../../../support/render'

interface Draft {
  name: string
  count: number
}

function read(draft: Draft): FormReading<'name', { name: string; count: number }> {
  const name = draft.name.trim()
  if (!name) return { problems: { name: 'common.imageRequired' }, value: null }
  return { problems: {}, value: { name, count: draft.count } }
}

async function mountForm() {
  const { result } = await mountComposable(() =>
    useForm({ initial: (): Draft => ({ name: '', count: 1 }), read }),
  )
  return result
}

describe('useForm', () => {
  it('shows no error until the form is submitted', async () => {
    const form = await mountForm()

    expect(form.errors.value).toEqual({})
    expect(form.invalid('name')).toBe(false)

    expect(form.submit()).toBeNull()

    expect(form.errors.value).toEqual({ name: t('common.imageRequired') })
    expect(form.invalid('name')).toBe(true)
  })

  it('marks a field refused without a message as invalid only', async () => {
    const { result: form } = await mountComposable(() =>
      useForm({
        initial: () => ({ title: '' }),
        read: (draft): FormReading<'title', string> =>
          draft.title
            ? { problems: {}, value: draft.title }
            : { problems: { title: true }, value: null },
      }),
    )

    form.submit()

    expect(form.invalid('title')).toBe(true)
    expect(form.errors.value).toEqual({})
  })

  it('returns the value read from the draft once it is valid', async () => {
    const form = await mountForm()
    form.draft.name = '  Acme '
    form.draft.count = 3

    expect(form.submit()).toEqual({ name: 'Acme', count: 3 })
    expect(form.errors.value).toEqual({})
  })

  it('resets the draft to the initial values plus the ones given and hides errors', async () => {
    const form = await mountForm()
    form.submit()
    form.draft.count = 9

    form.reset({ name: 'Globex' })

    expect(form.draft).toEqual({ name: 'Globex', count: 1 })
    expect(form.submitted.value).toBe(false)
    expect(form.errors.value).toEqual({})
  })
})
