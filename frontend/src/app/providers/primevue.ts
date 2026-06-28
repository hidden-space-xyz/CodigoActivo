import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'
import type { PrimeVueConfiguration } from 'primevue/config'
import PrimeVue from 'primevue/config'

import 'primeicons/primeicons.css'
import '@/assets/styles/primevue-overrides.css'

const CodigoActivoPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#e6fbfb',
      100: '#c2f4f5',
      200: '#9aecee',
      300: '#6fe3e6',
      400: '#4dd9dd',
      500: '#2dd4d9',
      600: '#22b3b8',
      700: '#198d91',
      800: '#11686b',
      900: '#0b4749',
      950: '#06292a',
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
