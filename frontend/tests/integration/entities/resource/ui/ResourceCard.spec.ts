import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { ResourceCard } from '@/entities/resource'

import { buildResourceSummary } from '../../../../support/fixtures/entities/models'
import { renderWithProviders, t } from '../../../../support/render'

describe('ResourceCard', () => {
  it('links an internal resource to its detail page', async () => {
    const resource = buildResourceSummary()
    const { wrapper, router } = await renderWithProviders(ResourceCard, { props: { resource } })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe('/resources/resource-1')
    expect(wrapper.find('h3').text()).toBe('Guía de Scratch')
    expect(wrapper.find('.resource-card__subtitle').text()).toBe('Primeros pasos')
    expect(wrapper.find('time').text()).toContain(t('entities.resource.card.dateLabel'))
    expect(wrapper.find('time').text()).toContain('1 feb 2026')
    expect(wrapper.find('img').attributes('src')).toBe('/api/files/thumb-r/content')

    await link.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('resource-detail')
    expect(router.currentRoute.value.params).toEqual({ resourceId: 'resource-1' })
  })

  it('renders an external resource as a plain anchor to its url', async () => {
    const resource = buildResourceSummary({ url: 'https://scratch.mit.edu', subtitle: '' })
    const { wrapper, router } = await renderWithProviders(ResourceCard, { props: { resource } })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe('https://scratch.mit.edu')
    expect(link.attributes('to')).toBeUndefined()
    expect(wrapper.find('.resource-card__subtitle').exists()).toBe(false)
    expect(router.currentRoute.value.fullPath).toBe('/')
  })
})
