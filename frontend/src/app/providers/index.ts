import type { App } from 'vue'
import ConfirmationService from 'primevue/confirmationservice'
import ToastService from 'primevue/toastservice'
import Tooltip from 'primevue/tooltip'

import { primevue } from './primevue'
import { queryClient } from './query-client'
import { router } from '@/app/router'

import { VueQueryPlugin } from '@tanstack/vue-query'

export function registerProviders(app: App): void {
  app.use(primevue.plugin, primevue.options)
  app.use(ToastService)
  app.use(ConfirmationService)
  app.use(VueQueryPlugin, { queryClient })
  app.use(router)
  app.directive('tooltip', Tooltip)
}
