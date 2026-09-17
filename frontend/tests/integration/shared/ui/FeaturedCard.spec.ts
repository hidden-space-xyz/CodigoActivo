import { describe, expect, it, vi } from 'vitest'

import { FeaturedCard } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

const baseProps = {
  badge: 'Destacado',
  title: 'Hackathon 2025',
  subtitle: 'Code all night',
  thumbnailId: 'poster-1',
  to: { name: 'event-detail', params: { eventId: 'event-1' } },
  ctaLabel: 'See event',
  tags: [
    { id: 'tag-1', name: 'Robotics', color: '#112233' },
    { id: 'tag-2', name: 'Web', color: '#ffffff' },
  ],
  meta: [
    { label: 'Date', value: '15 ene 2025' },
    { label: 'Place', value: 'Madrid' },
  ],
}

describe('FeaturedCard', () => {
  it('renders the highlight with tags, meta, poster and a call to action', async () => {
    const { wrapper, router } = await renderWithProviders(FeaturedCard, { props: baseProps })

    expect(wrapper.find('.featured__badge').text()).toBe('Destacado')
    expect(wrapper.find('h2').text()).toBe('Hackathon 2025')
    expect(wrapper.find('.featured__slogan').text()).toBe('Code all night')
    expect(wrapper.findAll('.featured__cats .el-tag').map((tag) => tag.text())).toEqual([
      'Robotics',
      'Web',
    ])
    expect(
      wrapper
        .findAll('.featured__meta-item')
        .map((item) => [
          item.find('.featured__meta-label').text(),
          item.find('.featured__meta-value').text(),
        ]),
    ).toEqual([
      ['Date', '15 ene 2025'],
      ['Place', 'Madrid'],
    ])
    const poster = wrapper.find('img')
    expect(poster.attributes('src')).toBe('/api/files/poster-1/content')
    expect(poster.attributes('alt')).toBe('Hackathon 2025')

    const cta = wrapper.find('a.featured__cta')
    expect(cta.text()).toBe('See event')
    expect(cta.attributes('href')).toBe('/events/event-1')
    await cta.trigger('click')
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('event-detail'))
  })

  it('hides optional sections when their data is empty', async () => {
    const { wrapper } = await renderWithProviders(FeaturedCard, {
      props: { ...baseProps, subtitle: '', thumbnailId: '', tags: [], meta: [] },
    })

    expect(wrapper.find('.featured__slogan').exists()).toBe(false)
    expect(wrapper.find('.featured__cats').exists()).toBe(false)
    expect(wrapper.find('.featured__meta').exists()).toBe(false)
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('.featured__poster').exists()).toBe(true)
  })
})
