import ts from 'typescript'
import { describe, expect, it } from 'vitest'

import * as sharedConfig from '@/shared/config'

import { nodesOf, sourceFiles } from './codebase'

interface LocaleUse {
  readonly path: string
  readonly callee: string
  readonly locale: ts.Expression | undefined
}

const localeArgumentIndex = new Map([
  ['localeCompare', 1],
  ['toLocaleDateString', 0],
  ['toLocaleLowerCase', 0],
  ['toLocaleString', 0],
  ['toLocaleTimeString', 0],
  ['toLocaleUpperCase', 0],
])

const modules = sourceFiles.filter(
  ({ path }) => /\.(?:ts|vue)$/.test(path) && !path.startsWith('src/shared/config/'),
)

function stringsOf(value: unknown): string[] {
  if (typeof value === 'string') return [value]
  return value !== null && typeof value === 'object' ? Object.values(value).flatMap(stringsOf) : []
}

function localeUses(path: string, node: ts.Node): LocaleUse[] {
  if (!ts.isCallExpression(node) && !ts.isNewExpression(node)) return []
  const callee = node.expression
  if (!ts.isPropertyAccessExpression(callee)) return []
  const index =
    ts.isIdentifier(callee.expression) && callee.expression.text === 'Intl'
      ? 0
      : localeArgumentIndex.get(callee.name.text)
  return index === undefined
    ? []
    : [{ path, callee: callee.getText(), locale: node.arguments?.[index] }]
}

describe('shared config', () => {
  it('is the only place that writes the identity, locale and API policy texts', () => {
    const values = stringsOf(sharedConfig)
    expect(values).toContain(sharedConfig.LOCALE)

    const repeated = modules.flatMap(({ path, text }) =>
      values
        .filter((value) => ["'", '"', '`'].some((quote) => text.includes(quote + value + quote)))
        .map((value) => `${path}: ${value}`),
    )

    expect(repeated, 'Import the shared/config constant instead of repeating its value').toEqual([])
  })

  it('gives LOCALE to every locale-sensitive formatter and comparison', () => {
    const uses = modules.flatMap((file) =>
      nodesOf(file).flatMap((node) => localeUses(file.path, node)),
    )
    expect(uses).not.toHaveLength(0)

    expect(
      uses
        .filter(({ locale }) => !locale || !ts.isIdentifier(locale) || locale.text !== 'LOCALE')
        .map(({ path, callee }) => `${path}: ${callee}`),
      'Intl formatters, toLocale* methods and localeCompare take LOCALE from shared/config',
    ).toEqual([])
  })
})
