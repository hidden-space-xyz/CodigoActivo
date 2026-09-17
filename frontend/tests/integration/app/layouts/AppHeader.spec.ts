import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDrawer } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import AppHeader from '@/app/layouts/AppHeader.vue'
import { useSession } from '@/entities/session'

import { renderWithProviders, t } from '../../../support/render'
import { http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../support/server'

function linkTexts(wrapper: VueWrapper, selector: string): string[] {
  return wrapper.findAll(selector).map((link) => link.text())
}

function drawer(wrapper: VueWrapper) {
  return wrapper.findComponent(ElDrawer)
}

async function openMenu(wrapper: VueWrapper): Promise<HTMLElement> {
  await wrapper.find('.header__burger').trigger('click')
  let menu: HTMLElement | null = null
  await vi.waitFor(() => {
    menu = document.body.querySelector<HTMLElement>('.el-drawer .menu')
    expect(menu).not.toBeNull()
  })
  return menu as unknown as HTMLElement
}

function menuLinks(menu: HTMLElement): string[] {
  return [...menu.querySelectorAll('.menu__link')].map((link) => link.textContent?.trim() ?? '')
}

const primaryLabels = () =>
  ['nav.home', 'nav.announcements', 'nav.events', 'nav.resources', 'nav.about'].map((key) => t(key))

describe('AppHeader', () => {
  it('shows the public navigation with login and registration for guests', async () => {
    const { wrapper } = await renderWithProviders(AppHeader, { attach: true })

    expect(wrapper.find('.header__brand').attributes('aria-label')).toBe(t('layout.brandAria'))
    expect(wrapper.find('.header__brand').attributes('href')).toBe('/')
    expect(linkTexts(wrapper, '.header__nav .header__link')).toEqual(primaryLabels())
    expect(wrapper.find('.header__login').text()).toBe(t('common.login'))
    expect(wrapper.find('.header__login').attributes('href')).toBe('/login')
    expect(wrapper.find('.header__cta').text()).toBe(t('common.register'))
    expect(wrapper.find('.header__cta').attributes('href')).toBe('/register')
    expect(wrapper.find('.header__greeting').exists()).toBe(false)
    expect(wrapper.find('.theme-toggle').exists()).toBe(true)
    expect(wrapper.text()).not.toContain(t('common.myAccount'))
  })

  it('marks the current section as active, including event details under events', async () => {
    const { wrapper, router } = await renderWithProviders(AppHeader, {
      route: '/events/event-1',
      attach: true,
    })

    const active = () => linkTexts(wrapper, '.header__link--active')
    expect(active()).toEqual([t('nav.events')])

    await router.push('/about')
    expect(active()).toEqual([t('nav.about')])
  })

  it('greets signed-in users and links to their account without admin access', async () => {
    const { wrapper } = await renderWithProviders(AppHeader, {
      user: { firstName: 'Grace', isAdmin: false },
      route: '/account',
      attach: true,
    })

    expect(wrapper.find('.header__greeting').text()).toBe(t('common.greeting', { name: 'Grace' }))
    const nav = linkTexts(wrapper, '.header__nav .header__link')
    expect(nav).toContain(t('common.myAccount'))
    expect(nav).not.toContain(t('common.admin'))
    expect(linkTexts(wrapper, '.header__link--active')).toEqual([t('common.myAccount')])
    expect(wrapper.find('.header__login').exists()).toBe(false)
    expect(wrapper.find('button.header__cta').text()).toBe(t('common.logout'))
  })

  it('links administrators to the admin dashboard', async () => {
    const { wrapper } = await renderWithProviders(AppHeader, {
      user: { isAdmin: true },
      attach: true,
    })

    const admin = wrapper
      .findAll('.header__nav .header__link')
      .find((link) => link.text() === t('common.admin'))
    expect(admin?.attributes('href')).toBe('/admin/dashboard')
  })

  it('signs out, clears the session and goes to login', async () => {
    let csrf: string | null = null
    server.use(
      http.post('/api/auth/logout', ({ request }) => {
        csrf = request.headers.get('X-CSRF-TOKEN')
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper, router } = await renderWithProviders(AppHeader, {
      user: { firstName: 'Grace' },
      route: '/about',
      attach: true,
    })

    await wrapper.find('button.header__cta').trigger('click')

    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
    expect(csrf).toBe(TEST_CSRF_TOKEN)
    expect(useSession().isAuthenticated).toBe(false)
    expect(wrapper.find('.header__login').exists()).toBe(true)
  })

  describe('mobile menu', () => {
    it('opens a drawer with the guest navigation and actions', async () => {
      const { wrapper } = await renderWithProviders(AppHeader, { attach: true })
      const burger = wrapper.find('.header__burger')
      expect(burger.attributes('aria-label')).toBe(t('layout.openMenu'))
      expect(burger.attributes('aria-expanded')).toBe('false')

      const menu = await openMenu(wrapper)

      expect(burger.attributes('aria-expanded')).toBe('true')
      expect(document.body.querySelector('.el-drawer__title')?.textContent).toBe(
        t('layout.menuTitle'),
      )
      expect(menu.querySelector('.menu__greeting')).toBeNull()
      expect(menuLinks(menu)).toEqual(primaryLabels())
      expect(
        [...menu.querySelectorAll('.menu__actions a')].map((link) => link.getAttribute('href')),
      ).toEqual(['/register', '/login'])
    })

    it('shows the greeting, account and admin links for signed-in administrators', async () => {
      const { wrapper } = await renderWithProviders(AppHeader, {
        user: { firstName: 'Ada', isAdmin: true },
        route: '/account',
        attach: true,
      })

      const menu = await openMenu(wrapper)

      expect(menu.querySelector('.menu__greeting')?.textContent?.trim()).toBe(
        t('common.greeting', { name: 'Ada' }),
      )
      expect(menuLinks(menu)).toEqual([
        ...primaryLabels(),
        t('common.admin'),
        t('common.myAccount'),
      ])
      expect(menu.querySelector('.menu__link--active')?.textContent?.trim()).toBe(
        t('common.myAccount'),
      )
      expect(menu.querySelector('.menu__actions button')?.textContent?.trim()).toBe(
        t('common.logout'),
      )
    })

    it('hides the admin link from signed-in members', async () => {
      const { wrapper } = await renderWithProviders(AppHeader, { user: {}, attach: true })

      const menu = await openMenu(wrapper)

      expect(menuLinks(menu)).toEqual([...primaryLabels(), t('common.myAccount')])
    })

    it('closes after following a link', async () => {
      const { wrapper, router } = await renderWithProviders(AppHeader, {
        user: { isAdmin: true },
        attach: true,
      })

      for (const label of [t('nav.events'), t('common.admin'), t('common.myAccount')]) {
        const menu = await openMenu(wrapper)
        const link = [...menu.querySelectorAll<HTMLElement>('.menu__link')].find(
          (candidate) => candidate.textContent?.trim() === label,
        )
        link?.click()
        await flushPromises()
        expect(drawer(wrapper).props('modelValue')).toBe(false)
      }

      expect(router.currentRoute.value.name).toBe('account')
    })

    it('closes when the drawer asks to close', async () => {
      const { wrapper } = await renderWithProviders(AppHeader, { attach: true })
      await openMenu(wrapper)

      wrapper.findComponent(ElDrawer).vm.$emit('update:modelValue', false)
      await flushPromises()

      expect(drawer(wrapper).props('modelValue')).toBe(false)
      expect(wrapper.find('.header__burger').attributes('aria-expanded')).toBe('false')
    })

    it('closes when the route changes', async () => {
      const { wrapper, router } = await renderWithProviders(AppHeader, { attach: true })
      await openMenu(wrapper)

      await router.push('/resources')
      await flushPromises()

      expect(drawer(wrapper).props('modelValue')).toBe(false)
    })

    it('signs out from the drawer', async () => {
      server.use(http.post('/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
      const { wrapper, router } = await renderWithProviders(AppHeader, {
        user: {},
        route: '/events',
        attach: true,
      })
      const menu = await openMenu(wrapper)

      menu.querySelector<HTMLButtonElement>('.menu__actions button')?.click()

      await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('login'))
      expect(useSession().isAuthenticated).toBe(false)
    })
  })
})
