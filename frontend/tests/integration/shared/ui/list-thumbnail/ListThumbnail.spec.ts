import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'

describe('ListThumbnail', () => {
  it('lazily loads the stored image with its alt text', async () => {
    const { wrapper } = await renderWithProviders(ListThumbnail, {
      props: { thumbnailId: 'file-9', alt: 'Workshop' },
    })

    const image = wrapper.find('img')
    expect(image.attributes('src')).toBe('/api/files/file-9/thumbnail')
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

  it('fills a card frame by default and offers a table size that shows whole logos', async () => {
    const card = await renderWithProviders(ListThumbnail, { props: { thumbnailId: 'file-9' } })
    const table = await renderWithProviders(ListThumbnail, {
      props: { thumbnailId: 'file-9', size: 'table', fit: 'contain' },
    })

    expect(card.wrapper.classes()).toEqual(['list-thumb'])
    expect(table.wrapper.classes()).toEqual(
      expect.arrayContaining(['list-thumb', 'list-thumb--table', 'list-thumb--contain']),
    )
  })

  it('falls back to the placeholder when the image fails and retries a new file', async () => {
    const { wrapper } = await renderWithProviders(ListThumbnail, {
      props: { thumbnailId: 'file-9' },
    })

    await wrapper.find('img').trigger('error')

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('.list-thumb__placeholder').exists()).toBe(true)

    await wrapper.setProps({ thumbnailId: 'file-10' })

    expect(wrapper.find('img').attributes('src')).toBe('/api/files/file-10/thumbnail')
    expect(wrapper.find('.list-thumb__placeholder').exists()).toBe(false)
  })
})
