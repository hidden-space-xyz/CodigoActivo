import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDrawer } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import AdminLayout from '@/app/layouts/AdminLayout.vue'
import { useSession } from '@/entities/session'
import { ADMIN_NAV } from '@/shared/config'

import { renderWithProviders, t } from '../../../support/render'
import { http, HttpResponse, server } from '../../../support/server'

const slots = { default: '<section class="page-content">Dashboard body</section>' }

function renderLayout(route = '/admin/events') {
  return renderWithProviders(AdminLayout, {
    user: { firstName: 'Ada', isAdmin: true },
    route,
    slots,
    attach: true,
  })
}

async function openMenu(wrapper: VueWrapper): Promise<HTMLElement> {
  await wrapper.find('.admin__burger').trigger('click')
  let nav: HTMLElement | null = null
  await vi.waitFor(() => {
    nav = document.body.querySelector<HTMLElement>('.el-drawer .admin__nav')
    expect(nav).not.toBeNull()
  })
  return nav as unknown as HTMLElement
}

describe('AdminLayout', () => {
  it('renders the sidebar navigation, user bar and page content', async () => {
    const { wrapper } = await renderLayout()

    expect(wrapper.find('.admin__brand').text()).toBe(t('layout.brandAria'))
    expect(wrapper.find('.admin__brand').attributes('href')).toBe('/')
    const links = wrapper.findAll('.admin__sidebar .admin__link')
    expect(links.map((link) => link.text())).toEqual(ADMIN_NAV.map((item) => t(item.labelKey)))
    expect(links.map((link) => link.attributes('href'))).toEqual([
      '/admin/dashboard',
      '/admin/events',
      '/admin/news',
      '/admin/partners',
      '/admin/resources',
      '/admin/users',
      '/admin/catalogs',
    ])
    expect(wrapper.find('.admin__sidebar .admin__link--active').text()).toBe(t('adminNav.events'))
    expect(wrapper.find('.admin__home').text()).toBe(t('layout.adminGoToSite'))
    expect(wrapper.find('.admin__username').text()).toBe('Ada')
    expect(wrapper.find('.theme-toggle').exists()).toBe(true)
    expect(wrapper.find('.admin__main .page-content').text()).toBe('Dashboard body')
  })

  it('signs out and goes to login', async () => {
    let called = false
    server.use(
      http.post('/api/auth/logout', () => {
        called = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper, router } = await renderLayout()

    const logout = wrapper.find('.admin__logout')
    expect(logout.text()).toBe(t('common.logout'))
    await logout.trigger('click')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
    expect(called).toBe(true)
    expect(useSession().isAuthenticated).toBe(false)
  })

  it('opens a navigation drawer that closes after following a link', async () => {
    const { wrapper, router } = await renderLayout('/admin/dashboard')
    const burger = wrapper.find('.admin__burger')
    expect(burger.attributes('aria-label')).toBe(t('layout.openMenu'))
    expect(burger.attributes('aria-expanded')).toBe('false')

    const nav = await openMenu(wrapper)

    expect(burger.attributes('aria-expanded')).toBe('true')
    expect(document.body.querySelector('.el-drawer__title')?.textContent).toBe(
      t('layout.adminMenuTitle'),
    )
    const links = [...nav.querySelectorAll<HTMLAnchorElement>('.admin__link--drawer')]
    expect(links.map((link) => link.textContent?.trim())).toEqual([
      ...ADMIN_NAV.map((item) => t(item.labelKey)),
      t('layout.adminGoToSite'),
    ])

    links.find((link) => link.textContent?.trim() === t('adminNav.users'))?.click()
    await flushPromises()

    expect(wrapper.findComponent(ElDrawer).props('modelValue')).toBe(false)
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('admin-users'))
  })

  it('closes the drawer from the go-to-site link and on route changes', async () => {
    const { wrapper, router } = await renderLayout()

    const nav = await openMenu(wrapper)
    const home = [...nav.querySelectorAll<HTMLAnchorElement>('.admin__link--drawer')].at(-1)
    home?.click()
    await flushPromises()
    expect(wrapper.findComponent(ElDrawer).props('modelValue')).toBe(false)
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))

    await openMenu(wrapper)
    await router.push('/admin/partners')
    await flushPromises()

    expect(wrapper.findComponent(ElDrawer).props('modelValue')).toBe(false)
  })

  it('closes the drawer when it asks to close', async () => {
    const { wrapper } = await renderLayout()
    await openMenu(wrapper)

    wrapper.findComponent(ElDrawer).vm.$emit('update:modelValue', false)
    await flushPromises()

    expect(wrapper.findComponent(ElDrawer).props('modelValue')).toBe(false)
    expect(wrapper.find('.admin__burger').attributes('aria-expanded')).toBe('false')
  })
})
