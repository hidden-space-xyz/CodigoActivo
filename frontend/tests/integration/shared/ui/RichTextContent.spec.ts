import { describe, expect, it } from 'vitest'

import RichTextContent from '@/shared/ui/RichTextContent.vue'

import { renderWithProviders } from '../../../support/render'

describe('RichTextContent', () => {
  it('renders stored rich text as sanitized HTML', async () => {
    const content = JSON.stringify({
      type: 'doc',
      content: [
        {
          type: 'paragraph',
          content: [
            { type: 'text', text: 'Bold', marks: [{ type: 'bold' }] },
            {
              type: 'text',
              text: ' click',
              marks: [{ type: 'link', attrs: { href: 'javascript:alert(1)' } }],
            },
          ],
        },
        { type: 'script', content: [{ type: 'text', text: 'evil()' }] },
      ],
    })

    const { wrapper } = await renderWithProviders(RichTextContent, { props: { content } })

    const container = wrapper.find('.rich-text')
    expect(container.find('strong').text()).toBe('Bold')
    expect(container.find('a').exists()).toBe(false)
    expect(container.text()).toBe('Bold click')
  })

  it('shows plain text as a paragraph and renders nothing for empty content', async () => {
    const plain = await renderWithProviders(RichTextContent, {
      props: { content: 'Legacy <b>text</b>' },
    })
    const empty = await renderWithProviders(RichTextContent, { props: { content: null } })

    expect(plain.wrapper.find('p').text()).toBe('Legacy <b>text</b>')
    expect(plain.wrapper.find('b').exists()).toBe(false)
    expect(empty.wrapper.find('.rich-text').text()).toBe('')
  })
})
