import type { VueWrapper } from '@vue/test-utils'
import { ElTag } from 'element-plus'
import { describe, expect, it } from 'vitest'

import { ColorTag } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

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
})
