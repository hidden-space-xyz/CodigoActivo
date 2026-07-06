import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'
import type { PrimeVueConfiguration } from 'primevue/config'
import PrimeVue from 'primevue/config'

import 'primeicons/primeicons.css'
import '@/assets/styles/primevue-overrides.css'

const CodigoActivoPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#fff6ee',
      100: '#ffe9d6',
      200: '#ffcfa6',
      300: '#ffb075',
      400: '#ff9a4f',
      500: '#ff8c38',
      600: '#fb7518',
      700: '#cc5c10',
      800: '#a24713',
      900: '#833b15',
      950: '#471b07',
    },
  },
})

const options: PrimeVueConfiguration = {
  theme: {
    preset: CodigoActivoPreset,
    options: {
      darkModeSelector: '.ca-dark',
      cssLayer: {
        name: 'primevue',
        order: 'theme, base, primevue',
      },
    },
  },
  ripple: true,
}

export const primevue = {
  plugin: PrimeVue,
  options,
}
