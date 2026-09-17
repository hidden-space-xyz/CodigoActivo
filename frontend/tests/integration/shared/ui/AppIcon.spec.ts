import { describe, expect, it } from 'vitest'

import { AppIcon } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

describe('AppIcon', () => {
  it('renders an Element Plus icon hidden from assistive technology', async () => {
    const { wrapper } = await renderWithProviders(AppIcon, {
      props: { name: 'calendar', class: 'extra', title: 'ignored-by-screen-readers' },
    })

    const icon = wrapper.find('.el-icon')
    expect(icon.exists()).toBe(true)
    expect(icon.attributes('aria-hidden')).toBe('true')
    expect(icon.classes()).toEqual(expect.arrayContaining(['app-icon', 'extra']))
    expect(icon.classes()).not.toContain('app-icon--spin')
    expect(icon.find('svg').exists()).toBe(true)
  })

  it('renders custom icons with fill and stroke paths', async () => {
    const { wrapper } = await renderWithProviders(AppIcon, { props: { name: 'list' } })

    const paths = wrapper.findAll('path')
    expect(paths.filter((path) => path.attributes('fill') === 'currentColor')).toHaveLength(3)
    expect(paths.filter((path) => path.attributes('stroke') === 'currentColor')).toHaveLength(3)
  })

  it('renders stroke-only and fill-only custom icons', async () => {
    const stroke = await renderWithProviders(AppIcon, { props: { name: 'bars' } })
    const fill = await renderWithProviders(AppIcon, { props: { name: 'facebook' } })

    expect(stroke.wrapper.findAll('path[fill="none"]')).toHaveLength(3)
    expect(fill.wrapper.findAll('path[fill="currentColor"]')).toHaveLength(1)
  })

  it('adds the spin class to both icon kinds', async () => {
    const element = await renderWithProviders(AppIcon, { props: { name: 'spinner', spin: true } })
    const custom = await renderWithProviders(AppIcon, { props: { name: 'ban', spin: true } })

    expect(element.wrapper.find('.el-icon').classes()).toContain('app-icon--spin')
    expect(custom.wrapper.find('.el-icon').classes()).toContain('app-icon--spin')
  })

  it('renders nothing for an unknown name', async () => {
    const { wrapper } = await renderWithProviders(AppIcon, { props: { name: 'does-not-exist' } })

    expect(wrapper.find('.el-icon').exists()).toBe(false)
    expect(wrapper.html()).not.toContain('svg')
  })
})
