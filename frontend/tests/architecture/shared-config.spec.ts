import ts from 'typescript'
import { describe, expect, it } from 'vitest'

import * as sharedConfig from '@/shared/config'

import { nodesOf, type SourceFile, sourceFiles, templateElements } from './codebase'

interface LocaleUse {
  readonly path: string
  readonly callee: string
  readonly locale: ts.Expression | undefined
}

interface LimitBinding {
  readonly written: string
  readonly allowed: boolean
}

const LENGTH_ATTRIBUTES = new Set(['maxlength', 'minlength'])
const LIMIT_ATTRIBUTES = new Set([...LENGTH_ATTRIBUTES, 'min', 'max'])

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

function sharedConfigImports(file: SourceFile): Set<string> {
  return new Set(
    nodesOf(file).flatMap((node) =>
      ts.isImportDeclaration(node) &&
      ts.isStringLiteral(node.moduleSpecifier) &&
      node.moduleSpecifier.text === '@/shared/config' &&
      node.importClause?.namedBindings &&
      ts.isNamedImports(node.importClause.namedBindings)
        ? node.importClause.namedBindings.elements.map(({ name }) => name.text)
        : [],
    ),
  )
}

function containsNumber(expression: string): boolean {
  const visit = (node: ts.Node): boolean =>
    ts.isNumericLiteral(node) || (ts.forEachChild(node, visit) ?? false)
  return visit(ts.createSourceFile('limit.ts', expression, ts.ScriptTarget.Latest, true))
}

function limitBindings(file: SourceFile): LimitBinding[] {
  const imported = sharedConfigImports(file)
  return templateElements(file).flatMap(({ element }) =>
    element.props.flatMap((prop): LimitBinding[] => {
      const written = `${file.path}: ${prop.loc.source}`
      if (!('exp' in prop))
        return LIMIT_ATTRIBUTES.has(prop.name) ? [{ written, allowed: false }] : []
      const attribute = prop.name === 'bind' ? prop.arg?.loc.source : undefined
      if (attribute === undefined || !LIMIT_ATTRIBUTES.has(attribute)) return []
      const expression = prop.exp?.loc.source ?? ''
      const allowed = LENGTH_ATTRIBUTES.has(attribute)
        ? imported.has(expression)
        : !containsNumber(expression)
      return [{ written, allowed }]
    }),
  )
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

  it('takes the input limits of templates from shared/config', () => {
    const bindings = modules.filter(({ path }) => path.endsWith('.vue')).flatMap(limitBindings)
    expect(bindings).not.toHaveLength(0)

    expect(
      bindings.filter(({ allowed }) => !allowed).map(({ written }) => written),
      'Bind maxlength and minlength to a shared/config constant and write no number in min or max',
    ).toEqual([])
  })
})
