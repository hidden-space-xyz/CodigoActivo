import { describe, expect, it } from 'vitest'

import { readNewsDraft, toNewsDraft } from '@/pages/admin/news/model/news-form'
import { EMPTY_DOC_JSON } from '@/shared/lib/rich-text'

import { richText } from '../../../../../support/builders'
import { buildNewsItem } from '../../../../../support/models'

describe('toNewsDraft', () => {
  it('starts blank', () => {
    expect(toNewsDraft(null)).toEqual({ title: '', subtitle: '', description: '' })
  })

  it('fills the draft from the news item being edited', () => {
    expect(toNewsDraft(buildNewsItem())).toEqual({
      title: 'Abrimos inscripciones',
      subtitle: 'Plazas limitadas',
      description: richText('Ya puedes apuntarte.'),
    })
  })
})

describe('readNewsDraft', () => {
  it('refuses a blank title and subtitle without a message', () => {
    expect(readNewsDraft({ title: ' ', subtitle: '', description: '' })).toEqual({
      problems: { title: true, subtitle: true },
      value: null,
    })
  })

  it('trims the text and saves an empty description as an empty document', () => {
    expect(readNewsDraft({ title: ' Demo day ', subtitle: ' Friday ', description: ' ' })).toEqual({
      problems: {},
      value: { title: 'Demo day', subtitle: 'Friday', description: EMPTY_DOC_JSON },
    })
    expect(
      readNewsDraft({ title: 'Demo', subtitle: 'Friday', description: richText('Body') }).value
        ?.description,
    ).toBe(richText('Body'))
  })
})
