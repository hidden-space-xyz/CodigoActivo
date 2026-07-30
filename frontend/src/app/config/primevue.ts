import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'
import type { PrimeVueConfiguration } from 'primevue/config'
import PrimeVue from 'primevue/config'

import 'primeicons/primeicons.css'
import '@/assets/styles/primevue-overrides.css'

import { primevueEs } from '@/shared/i18n'

const CodigoActivoPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#fff8ed',
      100: '#fdeccb',
      200: '#fbd68e',
      300: '#f9bd51',
      400: '#f9ac2f',
      500: '#f9a320',
      600: '#e58e0f',
      700: '#be6f10',
      800: '#975715',
      900: '#7c4817',
      950: '#452507',
    },
  },
})

const options: PrimeVueConfiguration = {
  locale: primevueEs,
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
