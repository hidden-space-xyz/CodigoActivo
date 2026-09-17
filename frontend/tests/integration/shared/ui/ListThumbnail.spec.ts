import { describe, expect, it } from 'vitest'

import { ListThumbnail } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

describe('ListThumbnail', () => {
  it('lazily loads the stored image with its alt text', async () => {
    const { wrapper } = await renderWithProviders(ListThumbnail, {
      props: { thumbnailId: 'file-9', alt: 'Workshop' },
    })

    const image = wrapper.find('img')
    expect(image.attributes('src')).toBe('/api/files/file-9/content')
    expect(image.attributes('alt')).toBe('Workshop')
    expect(image.attributes('loading')).toBe('lazy')
    expect(wrapper.find('.list-thumb__placeholder').exists()).toBe(false)
  })

  it('treats the image as decorative without alt text', async () => {
    const { wrapper } = await renderWithProviders(ListThumbnail, {
      props: { thumbnailId: 'file-9' },
    })

    expect(wrapper.find('img').attributes('alt')).toBe('')
  })

  it('shows a placeholder icon without a file', async () => {
    const { wrapper } = await renderWithProviders(ListThumbnail, { props: { thumbnailId: null } })

    expect(wrapper.find('img').exists()).toBe(false)
    const placeholder = wrapper.find('.list-thumb__placeholder')
    expect(placeholder.attributes('aria-hidden')).toBe('true')
    expect(placeholder.find('.app-icon').exists()).toBe(true)
  })
})
