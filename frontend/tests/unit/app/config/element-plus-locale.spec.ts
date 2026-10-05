import dayjs from 'dayjs'
import en from 'element-plus/es/locale/lang/en'
import { describe, expect, it } from 'vitest'

import { elementPlusLocale } from '@/app/config/element-plus-locale'

function leaves(value: unknown, path = ''): [string, string][] {
  if (typeof value === 'string') return [[path, value]]
  if (!value || typeof value !== 'object') return []
  return Object.entries(value).flatMap(([key, child]) =>
    leaves(child, path ? `${path}.${key}` : key),
  )
}

describe('elementPlusLocale', () => {
  it('is the Spanish locale with the accessible labels translated', () => {
    expect(elementPlusLocale.name).toBe('es')
    expect(elementPlusLocale.el.dialog.close).toBe('Cerrar este diálogo')
    expect(elementPlusLocale.el.messagebox.close).toBe('Cerrar este diálogo')
    expect(elementPlusLocale.el.table.sortLabel).toBe('Ordenar por {column}')
    expect(elementPlusLocale.el.pagination.next).toBe('Ir a la página siguiente')
  })

  it('leaves no sentence in English besides a developer warning', () => {
    const english = new Map(leaves(en.el))
    const untranslated = leaves(elementPlusLocale.el).filter(
      ([path, text]) => english.get(path) === text && /[a-z]{3}/i.test(text),
    )

    expect(untranslated.map(([path]) => path)).toEqual([
      'pagination.total',
      'pagination.deprecationWarning',
    ])
  })

  it('starts the weeks of date pickers on Monday', () => {
    expect(dayjs.Ls.es?.weekStart).toBe(1)
  })
})
