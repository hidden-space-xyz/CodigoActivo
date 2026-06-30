import { defineConfig } from 'steiger'
import fsd from '@feature-sliced/steiger-plugin'

export default defineConfig([
  ...fsd.configs.recommended,
  {
    // Orval output is generated code; it does not follow FSD slicing.
    ignores: ['**/shared/api/generated/**'],
  },
  {
    // Keep layers uniform (every domain has entity/feature/page) instead of
    // dissolving single-consumer slices into their consumer. This is the most
    // commonly disabled FSD rule: it penalizes preparatory slicing and would
    // make parallel "manage-*" admin features structurally inconsistent.
    rules: {
      'fsd/insignificant-slice': 'off',
    },
  },
])
