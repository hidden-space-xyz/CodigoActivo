import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { PageHeading } from '@/shared/ui/page-heading'

describe('PageHeading', () => {
  it('renders the title and the comment-styled description', async () => {
    const { wrapper } = await renderWithProviders(PageHeading, {
      props: { title: 'Resources', description: 'Learning material' },
    })

    expect(wrapper.find('h1').text()).toBe('Resources')
    const mark = wrapper.find('.page-heading__comment-mark')
    expect(mark.text()).toBe('//')
    expect(mark.attributes('aria-hidden')).toBe('true')
    expect(wrapper.find('.page-heading__comment').text()).toContain('Learning material')
  })
})
