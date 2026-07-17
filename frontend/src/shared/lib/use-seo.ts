import { toValue, watchEffect, type MaybeRefOrGetter } from 'vue'
import { useRoute, type RouteLocationNormalized } from 'vue-router'

import { i18n } from '@/shared/i18n'

export interface SeoRouteMeta {
  readonly titleKey?: string | undefined
  readonly descriptionKey?: string | undefined
  readonly noindex?: boolean | undefined
}

export interface SeoData {
  readonly title?: string | undefined
  readonly description?: string | undefined
  readonly noindex?: boolean | undefined
  readonly image?: string | undefined
  readonly type?: 'website' | 'article' | undefined
  readonly jsonLd?: Record<string, unknown> | undefined
}

const JSON_LD_ID = 'ca-jsonld'

export function absoluteUrl(path: string): string {
  return new URL(path, window.location.origin).href
}

function upsertMeta(attribute: 'name' | 'property', key: string, content: string): void {
  let element = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${key}"]`)
  if (!element) {
    element = document.createElement('meta')
    element.setAttribute(attribute, key)
    document.head.append(element)
  }
  element.setAttribute('content', content)
}

function removeMeta(attribute: 'name' | 'property', key: string): void {
  document.head.querySelector(`meta[${attribute}="${key}"]`)?.remove()
}

function setCanonical(href: string | null): void {
  let element = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')
  if (!href) {
    element?.remove()
    return
  }
  if (!element) {
    element = document.createElement('link')
    element.setAttribute('rel', 'canonical')
    document.head.append(element)
  }
  element.setAttribute('href', href)
}

function setJsonLd(jsonLd: Record<string, unknown> | undefined): void {
  let element = document.getElementById(JSON_LD_ID)
  if (!jsonLd) {
    element?.remove()
    return
  }
  if (!element) {
    element = document.createElement('script')
    element.setAttribute('type', 'application/ld+json')
    element.id = JSON_LD_ID
    document.head.append(element)
  }
  element.textContent = JSON.stringify(jsonLd)
}

function resolveRouteSeo(meta: SeoRouteMeta): SeoData {
  return {
    title: meta.titleKey ? i18n.global.t(meta.titleKey) : undefined,
    description: meta.descriptionKey ? i18n.global.t(meta.descriptionKey) : undefined,
    noindex: meta.noindex,
  }
}

function applySeo(path: string, seo: SeoData): void {
  const siteName = i18n.global.t('seo.siteName')
  const title = seo.title
    ? `${seo.title}${i18n.global.t('seo.titleSeparator')}${siteName}`
    : i18n.global.t('seo.defaultTitle')
  const description = seo.description ?? i18n.global.t('seo.defaultDescription')
  const url = absoluteUrl(path)
  const image = absoluteUrl(seo.image || '/og-image.png')

  document.title = title
  upsertMeta('name', 'description', description)
  setCanonical(seo.noindex ? null : url)
  if (seo.noindex) upsertMeta('name', 'robots', 'noindex')
  else removeMeta('name', 'robots')
  upsertMeta('property', 'og:title', title)
  upsertMeta('property', 'og:description', description)
  upsertMeta('property', 'og:type', seo.type ?? 'website')
  upsertMeta('property', 'og:url', url)
  upsertMeta('property', 'og:image', image)
  upsertMeta('name', 'twitter:title', title)
  upsertMeta('name', 'twitter:description', description)
  upsertMeta('name', 'twitter:image', image)
  setJsonLd(seo.jsonLd)
}

function withoutUndefined(seo: SeoData): SeoData {
  return Object.fromEntries(
    Object.entries(seo).filter(([, value]) => value !== undefined),
  ) as SeoData
}

export function applyRouteSeo(to: RouteLocationNormalized): void {
  const resolved = resolveRouteSeo(to.meta.seo ?? {})
  applySeo(to.path, to.meta.layout === 'admin' ? { ...resolved, noindex: true } : resolved)
}

export function useSeo(seo: MaybeRefOrGetter<SeoData | undefined>): void {
  const route = useRoute()
  const routeName = route.name

  watchEffect(() => {
    const value = toValue(seo)
    if (!value || route.name !== routeName) return
    applySeo(route.path, { ...resolveRouteSeo(route.meta.seo ?? {}), ...withoutUndefined(value) })
  })
}
