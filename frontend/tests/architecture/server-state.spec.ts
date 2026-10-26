import ts from 'typescript'
import { describe, expect, it } from 'vitest'

import { nodesOf, sliceFiles, sourceFiles } from './codebase'

interface OptionsCall {
  readonly path: string
  readonly factory: string
  readonly owner: string
}

const apiModules = new Set(
  sliceFiles.filter(({ inner }) => inner.startsWith('api/')).map(({ path }) => path),
)

function ownerOf(node: ts.Node): string {
  let statement = node
  while (!ts.isSourceFile(statement.parent)) statement = statement.parent
  return ts.isVariableStatement(statement)
    ? statement.declarationList.declarations.map(({ name }) => name.getText()).join(', ')
    : ts.SyntaxKind[statement.kind]
}

function callsTo(factories: readonly string[]): OptionsCall[] {
  return sourceFiles.flatMap((file) =>
    nodesOf(file)
      .filter(ts.isCallExpression)
      .flatMap(({ expression }) =>
        ts.isIdentifier(expression) && factories.includes(expression.text)
          ? [{ path: file.path, factory: expression.text, owner: ownerOf(expression) }]
          : [],
      ),
  )
}

function misplaced(calls: readonly OptionsCall[], ownerSuffix: string): string[] {
  return calls
    .filter(({ path, owner }) => !apiModules.has(path) || !owner.endsWith(ownerSuffix))
    .map(({ path, factory, owner }) => `${path}: ${factory} in ${owner}`)
}

describe('server state', () => {
  it('builds query options only inside the xQueries objects of api segments', () => {
    const calls = callsTo(['queryOptions', 'infiniteQueryOptions'])
    expect(calls).not.toHaveLength(0)

    expect(
      misplaced(calls, 'Queries'),
      'queryOptions belong to an `xQueries` object of an api segment',
    ).toEqual([])
  })

  it('builds mutation options only inside the xMutations objects of api segments', () => {
    const calls = callsTo(['mutationOptions'])
    expect(calls).not.toHaveLength(0)

    expect(
      misplaced(calls, 'Mutations'),
      'mutationOptions belong to an `xMutations` object of an api segment',
    ).toEqual([])
  })
})
