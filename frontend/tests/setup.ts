import { Blob as NodeBlob, File as NodeFile } from 'node:buffer'

import { enableAutoUnmount } from '@vue/test-utils'
import { afterAll, afterEach, beforeAll, vi } from 'vitest'

import { useSession } from '@/entities/session'
import { resetCsrfToken } from '@/shared/api'

import { matchMediaMock, resetMediaQueries } from './support/media'
import { server } from './support/server'

installBrowserPolyfills()

// Multipart uploads use Node's Blob, File and FormData so MSW receives the real file part: jsdom's
// FormData stringifies Node files and Node's fetch rejects jsdom files. The properties are redefined
// on the global object only; assigning them would also replace jsdom's `window` classes, which
// Vitest's jsdom Request compatibility layer relies on.
const nodeFormData = await new Response(new URLSearchParams('probe=1')).formData()
defineGlobal('Blob', NodeBlob)
defineGlobal('File', NodeFile)
defineGlobal('FormData', nodeFormData.constructor)

function defineGlobal(name: 'Blob' | 'File' | 'FormData', value: unknown): void {
  Object.defineProperty(globalThis, name, { configurable: true, writable: true, value })
}

beforeAll(() => {
  // Any request without an explicit handler fails the test, so no test can reach a real backend.
  server.listen({ onUnhandledRequest: 'error' })
})

enableAutoUnmount(afterEach)

afterEach(() => {
  server.resetHandlers()
  useSession().clear()
  resetCsrfToken()
  vi.useRealTimers()
  resetMediaQueries()
  localStorage.clear()
  sessionStorage.clear()
  document.body.innerHTML = ''
  document.documentElement.className = ''
  // SEO composables write title, meta, canonical and JSON-LD tags into the head.
  document.title = ''
  document.head
    .querySelectorAll('meta, link[rel="canonical"], script[type="application/ld+json"]')
    .forEach((element) => element.remove())
})

afterAll(() => {
  server.close()
})

function installBrowserPolyfills(): void {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: matchMediaMock,
  })

  class NoopObserver {
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
    takeRecords(): [] {
      return []
    }
  }

  window.ResizeObserver ??= NoopObserver as unknown as typeof ResizeObserver
  window.IntersectionObserver ??= NoopObserver as unknown as typeof IntersectionObserver

  window.scrollTo = () => undefined
  Element.prototype.scrollIntoView = () => undefined
  Element.prototype.scrollTo = () => undefined

  // ProseMirror measures ranges and hit-tests coordinates, which jsdom does not implement.
  const emptyRects = (): DOMRectList => Object.assign([], { item: () => null })
  const emptyRect = (): DOMRect => new DOMRect(0, 0, 0, 0)
  Range.prototype.getClientRects = emptyRects
  Range.prototype.getBoundingClientRect = emptyRect
  Element.prototype.getClientRects = emptyRects
  document.elementFromPoint = () => null

  URL.createObjectURL = () => 'blob:test-object-url'
  URL.revokeObjectURL = () => undefined

  // Chart.js and canvas consumers only need a context object; jsdom has no canvas backend.
  HTMLCanvasElement.prototype.getContext = (() => null) as HTMLCanvasElement['getContext']
}
