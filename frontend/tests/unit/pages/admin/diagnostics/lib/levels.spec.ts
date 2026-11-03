import { describe, expect, it } from 'vitest'

import { DIAGNOSTIC_LEVELS, levelLabelKey } from '@/pages/admin/diagnostics/lib/levels'

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
})
