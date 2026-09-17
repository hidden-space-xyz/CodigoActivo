import { describe, expect, it } from 'vitest'

import DefaultLayout from '@/app/layouts/DefaultLayout.vue'

import { renderWithProviders } from '../../../support/render'

describe('DefaultLayout', () => {
  it('wraps the page between the background, header and footer', async () => {
    const { wrapper } = await renderWithProviders(DefaultLayout, {
      slots: { default: '<p class="page">Page body</p>' },
    })

    const children = wrapper.find('.layout').element.children
    expect([...children].map((child) => child.tagName.toLowerCase())).toEqual([
      'div',
      'header',
      'main',
      'footer',
    ])
    expect(wrapper.find('.bg').exists()).toBe(true)
    expect(wrapper.find('main.layout__main .page').text()).toBe('Page body')
  })
})
