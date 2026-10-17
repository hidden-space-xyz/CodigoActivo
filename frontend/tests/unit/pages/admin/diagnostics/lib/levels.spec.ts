import { describe, expect, it } from 'vitest'

import {
  DIAGNOSTIC_LEVELS,
  levelLabelKey,
  levelTagType,
} from '@/pages/admin/diagnostics/lib/levels'

import { t } from '../../../../../support/render'

describe('diagnostic levels', () => {
  it('names every level in Spanish', () => {
    expect(DIAGNOSTIC_LEVELS.map((level) => t(levelLabelKey(level)))).toEqual([
      'Traza',
      'Depuración',
      'Información',
      'Aviso',
      'Error',
      'Crítico',
    ])
  })

  it('colours failures red and warnings amber', () => {
    expect(DIAGNOSTIC_LEVELS.map(levelTagType)).toEqual([
      'info',
      'info',
      'primary',
      'warning',
      'danger',
      'danger',
    ])
  })
})
