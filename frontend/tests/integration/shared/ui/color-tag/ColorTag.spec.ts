import type { VueWrapper } from '@vue/test-utils'
import { ElTag } from 'element-plus'
import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { ColorTag } from '@/shared/ui/color-tag'

function tagVars(wrapper: VueWrapper) {
  const tag = wrapper.find('.el-tag')
  const style = (wrapper.findComponent(ElTag).vm.$attrs.style ?? {}) as Record<string, string>
  return {
    classes: tag.classes(),
    text: tag.text(),
    background: style['--el-tag-bg-color'] ?? '',
    color: style['--el-tag-text-color'] ?? '',
    border: style['--el-tag-border-color'] ?? '',
  }
}

describe('ColorTag', () => {
  it('uses white text on dark backgrounds without a border', async () => {
    const { wrapper } = await renderWithProviders(ColorTag, {
      props: { value: 'Robotics', color: '#123' },
    })

    const tag = tagVars(wrapper)
    expect(tag.text).toBe('Robotics')
    expect(tag.background).toBe('#112233')
    expect(tag.color).toBe('#ffffff')
    expect(tag.border).toBe('transparent')
    expect(tag.classes).not.toContain('el-tag--info')
  })

  it('uses dark text on light backgrounds', async () => {
    const { wrapper } = await renderWithProviders(ColorTag, {
      props: { value: 'Design', color: '#ffcc00' },
    })

    const tag = tagVars(wrapper)
    expect(tag.color).toBe('#1f2937')
    expect(tag.border).toBe('transparent')
  })

  it('adds a subtle border to very light backgrounds', async () => {
    const { wrapper } = await renderWithProviders(ColorTag, {
      props: { value: 'Blank', color: '#FAFAFA' },
    })

    expect(tagVars(wrapper).border).toBe('rgba(0, 0, 0, 0.15)')
  })

  it('falls back to the neutral info tag for missing or invalid colors', async () => {
    const missing = await renderWithProviders(ColorTag, { props: { value: 'None' } })
    const invalid = await renderWithProviders(ColorTag, {
      props: { value: 'Bad', color: 'purple' },
    })

    for (const { wrapper } of [missing, invalid]) {
      const tag = tagVars(wrapper)
      expect(tag.classes).toContain('el-tag--info')
      expect(tag.background).toBe('')
    }
  })

  it('renders both solid tags without the appear transition', async () => {
    const colored = await renderWithProviders(ColorTag, {
      props: { value: 'Robotics', color: '#123' },
    })
    const neutral = await renderWithProviders(ColorTag, { props: { value: 'None' } })

    for (const { wrapper } of [colored, neutral]) {
      expect(wrapper.findComponent(ElTag).props('disableTransitions')).toBe(true)
      expect(wrapper.find('.el-tag').classes()).not.toContain('color-tag--truncate')
      expect(wrapper.find('.el-tag').attributes('title')).toBeUndefined()
    }
  })

  it('keeps a truncated tag on one line with its full text as tooltip', async () => {
    const colored = await renderWithProviders(ColorTag, {
      props: { value: 'Robotics and electronics', color: '#123', truncate: true },
    })
    const neutral = await renderWithProviders(ColorTag, {
      props: { value: 'Robotics and electronics', truncate: true },
    })

    for (const { wrapper } of [colored, neutral]) {
      const tag = wrapper.find('.el-tag')
      expect(tag.classes()).toContain('color-tag--truncate')
      expect(tag.attributes('title')).toBe('Robotics and electronics')
    }
  })

  it('renders a coloured dot before plain text in the dot variant', async () => {
    const { wrapper } = await renderWithProviders(ColorTag, {
      props: { value: 'Active', color: '#2E9E57', variant: 'dot' },
    })

    expect(wrapper.find('.el-tag').exists()).toBe(false)
    const root = wrapper.find('span.color-tag--dot')
    expect(root.attributes('title')).toBe('Active')
    expect(root.find('.color-tag__text').text()).toBe('Active')
    const dot = root.find('.color-tag__dot')
    expect(dot.attributes('aria-hidden')).toBe('true')
    expect((dot.element as HTMLElement).style.background).toBe('rgb(46, 158, 87)')
  })

  it('falls back to a neutral dot for an invalid colour', async () => {
    const { wrapper } = await renderWithProviders(ColorTag, {
      props: { value: 'Unknown', color: 'purple', variant: 'dot' },
    })

    expect(wrapper.find('.color-tag__dot').attributes('style')).toBeUndefined()
  })
})
