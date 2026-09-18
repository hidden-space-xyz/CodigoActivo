import type { App } from 'vue'

import { elementPlus } from './element-plus'
import { queryClient } from './query-client'
import { router } from '@/app/router'
import { i18n } from '@/shared/i18n'

import { VueQueryPlugin } from '@tanstack/vue-query'

export { registerStaleBuildReload } from './stale-build-reload'

/** Installs i18n, Element Plus, TanStack Query and the router on the app, in that order. */
export function registerProviders(app: App): void {
  app.use(i18n)
  app.use(elementPlus.plugin, elementPlus.options)
  app.use(VueQueryPlugin, { queryClient })
  app.use(router)
}
