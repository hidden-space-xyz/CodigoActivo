import { describe, expect, it } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'

import { router } from '@/app/router'

import { t } from '../../../support/render'

describe('router', () => {
  it('restores the saved position on back and forward navigation', async () => {
    const scrollBehavior = router.options.scrollBehavior
    if (!scrollBehavior) throw new Error('scrollBehavior is not configured')
    const location = {} as RouteLocationNormalized

    expect(await scrollBehavior(location, location, { left: 0, top: 480 })).toEqual({
      left: 0,
      top: 480,
    })
    expect(await scrollBehavior(location, location, null)).toEqual({
      top: 0,
      behavior: 'smooth',
    })
  })

  it('uses the browser history and applies route SEO after navigation', async () => {
    await router.push('/about')

    expect(router.currentRoute.value.name).toBe('about')
    expect(window.location.pathname).toBe('/about')
    expect(document.title).toContain(t('seo.routes.about.title'))
  })

  it('does not apply SEO for navigations that fail', async () => {
    await router.push('/about')
    document.title = 'unchanged'

    await router.push('/account')

    expect(router.currentRoute.value.name).toBe('login')
    expect(document.title).toContain(t('seo.routes.login.title'))

    document.title = 'unchanged'
    const failure = await router.push(router.currentRoute.value.fullPath)

    expect(failure).toBeTruthy()
    expect(document.title).toBe('unchanged')
  })
})
