import { nextTick, ref } from 'vue'
import { afterEach, describe, expect, it } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'

import { absoluteUrl, applyRouteSeo, useSeo, type SeoData } from '@/shared/lib'

import { withSetup } from '../../../support/fixtures/shared-app/with-setup'
import { t } from '../../../support/render'

function route(path: string, meta: RouteLocationNormalized['meta'] = {}): RouteLocationNormalized {
  return { path, meta } as RouteLocationNormalized
}

function meta(selector: string): string | null {
  return document.head.querySelector(selector)?.getAttribute('content') ?? null
}

function canonical(): string | null {
  return document.head.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null
}

function jsonLd(): string | null {
  return document.getElementById('ca-jsonld')?.textContent ?? null
}

afterEach(() => {
  document.head.innerHTML = ''
  document.title = ''
})

describe('absoluteUrl', () => {
  it('resolves paths against the current origin', () => {
    expect(absoluteUrl('/events')).toBe(`${window.location.origin}/events`)
    expect(absoluteUrl('https://cdn.test/a.png')).toBe('https://cdn.test/a.png')
  })
})

describe('applyRouteSeo', () => {
  it('uses the site defaults without route SEO', () => {
    applyRouteSeo(route('/'))

    const origin = window.location.origin
    expect(document.title).toBe(t('seo.defaultTitle'))
    expect(meta('meta[name="description"]')).toBe(t('seo.defaultDescription'))
    expect(canonical()).toBe(`${origin}/`)
    expect(meta('meta[name="robots"]')).toBeNull()
    expect(meta('meta[property="og:title"]')).toBe(t('seo.defaultTitle'))
    expect(meta('meta[property="og:description"]')).toBe(t('seo.defaultDescription'))
    expect(meta('meta[property="og:type"]')).toBe('website')
    expect(meta('meta[property="og:url"]')).toBe(`${origin}/`)
    expect(meta('meta[property="og:image"]')).toBe(`${origin}/og-image.png`)
    expect(meta('meta[name="twitter:title"]')).toBe(t('seo.defaultTitle'))
    expect(meta('meta[name="twitter:description"]')).toBe(t('seo.defaultDescription'))
    expect(meta('meta[name="twitter:image"]')).toBe(`${origin}/og-image.png`)
    expect(jsonLd()).toBeNull()
  })

  it('resolves the title and description keys from route meta', () => {
    applyRouteSeo(
      route('/about', {
        seo: { titleKey: 'seo.routes.about.title', descriptionKey: 'seo.routes.about.description' },
      }),
    )

    const title = `${t('seo.routes.about.title')}${t('seo.titleSeparator')}${t('seo.siteName')}`
    expect(document.title).toBe(title)
    expect(meta('meta[name="description"]')).toBe(t('seo.routes.about.description'))
    expect(meta('meta[property="og:title"]')).toBe(title)
  })

  it('adds noindex and removes the canonical for noindex routes, updating existing tags', () => {
    applyRouteSeo(route('/about'))
    expect(canonical()).not.toBeNull()

    applyRouteSeo(route('/login', { seo: { titleKey: 'seo.routes.login.title', noindex: true } }))

    expect(canonical()).toBeNull()
    expect(meta('meta[name="robots"]')).toBe('noindex')
    expect(document.head.querySelectorAll('meta[name="description"]')).toHaveLength(1)

    applyRouteSeo(route('/about'))
    expect(meta('meta[name="robots"]')).toBeNull()
    expect(canonical()).toBe(`${window.location.origin}/about`)
  })

  it('always marks admin layout routes as noindex', () => {
    applyRouteSeo(route('/admin/events', { layout: 'admin' }))

    expect(meta('meta[name="robots"]')).toBe('noindex')
    expect(canonical()).toBeNull()
  })
})

describe('useSeo', () => {
  it('overrides route SEO with page data and updates when the data changes', async () => {
    const seo = ref<SeoData | undefined>(undefined)
    await withSetup(() => useSeo(seo), { route: '/about' })

    expect(document.title).toBe('')

    seo.value = {
      title: 'Hackathon',
      description: undefined,
      image: '/api/files/1/content',
      type: 'article',
      jsonLd: { '@type': 'Event', name: 'Hackathon' },
    }
    await nextTick()

    expect(document.title).toBe(`Hackathon${t('seo.titleSeparator')}${t('seo.siteName')}`)
    expect(meta('meta[name="description"]')).toBe(t('seo.routes.about.description'))
    expect(meta('meta[property="og:type"]')).toBe('article')
    expect(meta('meta[property="og:image"]')).toBe(`${window.location.origin}/api/files/1/content`)
    expect(JSON.parse(jsonLd() ?? '{}')).toEqual({ '@type': 'Event', name: 'Hackathon' })

    seo.value = { title: 'Updated', noindex: true }
    await nextTick()

    expect(document.title).toBe(`Updated${t('seo.titleSeparator')}${t('seo.siteName')}`)
    expect(jsonLd()).toBeNull()
    expect(meta('meta[name="robots"]')).toBe('noindex')
  })

  it('falls back to the site defaults on routes without SEO meta and reuses the JSON-LD tag', async () => {
    const seo = ref<SeoData | undefined>({ jsonLd: { '@type': 'Organization', name: 'First' } })
    await withSetup(() => useSeo(seo), { route: '/' })

    expect(document.title).toBe(t('seo.defaultTitle'))
    expect(meta('meta[name="description"]')).toBe(t('seo.defaultDescription'))
    const tag = document.getElementById('ca-jsonld')
    expect(tag?.getAttribute('type')).toBe('application/ld+json')

    seo.value = { jsonLd: { '@type': 'Organization', name: 'Second' } }
    await nextTick()

    expect(document.getElementById('ca-jsonld')).toBe(tag)
    expect(document.head.querySelectorAll('#ca-jsonld')).toHaveLength(1)
    expect(JSON.parse(jsonLd() ?? '{}')).toEqual({ '@type': 'Organization', name: 'Second' })
  })

  it('stops applying once the user navigates to another route', async () => {
    const seo = ref<SeoData | undefined>({ title: 'First' })
    const { router } = await withSetup(() => useSeo(seo), { route: '/events' })
    expect(document.title).toContain('First')

    await router.push('/about')
    seo.value = { title: 'Late data' }
    await nextTick()

    expect(document.title).toContain('First')
  })
})
