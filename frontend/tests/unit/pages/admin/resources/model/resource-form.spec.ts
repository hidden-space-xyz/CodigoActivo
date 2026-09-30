import { describe, expect, it } from 'vitest'

import type { ResourceType } from '@/entities/resource'
import {
  readResourceDraft,
  toResourceDraft,
  type ResourceDraft,
} from '@/pages/admin/resources/model/resource-form'

import { richText } from '../../../../../support/builders'
import { buildLearningResource } from '../../../../../support/models'

const ARTICLE: ResourceType = { id: 'type-article', name: 'Artículo', color: '', isExternal: false }
const LINK: ResourceType = { id: 'type-link', name: 'Enlace', color: '', isExternal: true }
const TYPES = [ARTICLE, LINK]

function draftOf(overrides: Partial<ResourceDraft> = {}): ResourceDraft {
  return {
    title: ' Vue docs ',
    subtitle: ' Official ',
    resourceTypeId: 'type-article',
    description: richText('Body'),
    url: '',
    ...overrides,
  }
}

describe('toResourceDraft', () => {
  it('starts blank', () => {
    expect(toResourceDraft(null)).toEqual({
      title: '',
      subtitle: '',
      resourceTypeId: '',
      description: '',
      url: '',
    })
  })

  it('fills the draft from the resource being edited', () => {
    expect(toResourceDraft(buildLearningResource({ url: null }))).toEqual({
      title: 'Guía de Python',
      subtitle: 'Primeros pasos',
      resourceTypeId: 'type-article',
      description: richText('Todo sobre Python.'),
      url: '',
    })
  })
})

describe('readResourceDraft', () => {
  it('refuses a blank title and subtitle without a message and asks for a type', () => {
    expect(
      readResourceDraft(draftOf({ title: ' ', subtitle: '', resourceTypeId: '' }), TYPES),
    ).toEqual({
      problems: {
        title: true,
        subtitle: true,
        resourceTypeId: 'pages.admin.resources.form.problems.typeRequired',
      },
      value: null,
    })
  })

  it('refuses a type that is no longer available without a message', () => {
    expect(readResourceDraft(draftOf(), [LINK])).toEqual({
      problems: { resourceTypeId: true },
      value: null,
    })
  })

  it('reads an internal resource with its description and no link', () => {
    expect(readResourceDraft(draftOf({ url: 'https://ignored.test' }), TYPES)).toEqual({
      problems: {},
      value: {
        title: 'Vue docs',
        subtitle: 'Official',
        description: richText('Body'),
        url: null,
        resourceTypeId: 'type-article',
      },
    })
    expect(readResourceDraft(draftOf({ description: '' }), TYPES).problems).toEqual({
      description: 'pages.admin.resources.form.problems.descriptionRequired',
    })
  })

  it('reads an external resource with an http link and no description', () => {
    const external = draftOf({ resourceTypeId: 'type-link' })

    expect(readResourceDraft(external, TYPES).problems).toEqual({
      url: 'pages.admin.resources.form.problems.urlRequired',
    })
    for (const url of ['ftp://example.test', 'https://']) {
      expect(readResourceDraft({ ...external, url }, TYPES).problems).toEqual({
        url: 'pages.admin.resources.form.problems.urlInvalid',
      })
    }
    expect(readResourceDraft({ ...external, url: ' https://vuejs.org ' }, TYPES).value).toEqual({
      title: 'Vue docs',
      subtitle: 'Official',
      description: null,
      url: 'https://vuejs.org',
      resourceTypeId: 'type-link',
    })
  })
})
