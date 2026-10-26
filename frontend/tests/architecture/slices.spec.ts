import ts from 'typescript'
import { describe, expect, it } from 'vitest'

import { scriptsOf, sliceFiles, sourceFiles } from './codebase'

const segmentLayout = /^(?:index\.ts|(?:ui|model|api|lib|config)\/.+)$/
const crossImportLayout = /^@x\/[^/]+\.ts$/

function isReExport(statement: ts.Statement): boolean {
  return ts.isExportDeclaration(statement) && statement.moduleSpecifier !== undefined
}

describe('slices', () => {
  it('hold only their index.ts and the ui, model, api, lib and config segments', () => {
    expect(sliceFiles).not.toHaveLength(0)

    const misplaced = sliceFiles
      .filter(
        ({ path, slice, inner }) =>
          !slice ||
          !(
            segmentLayout.test(inner) ||
            (path.startsWith('src/entities/') && crossImportLayout.test(inner))
          ),
      )
      .map(({ path }) => path)

    expect(
      misplaced,
      'Every file of pages, widgets, features and entities belongs to a slice with an index.ts and sits in its ui, model, api, lib or config segment (entities may add @x/<slice>.ts)',
    ).toEqual([])
  })

  it('keep Vue components, and only components, in their ui segment', () => {
    expect(sliceFiles.filter(({ path }) => path.endsWith('.vue'))).not.toHaveLength(0)

    const misplaced = sliceFiles
      .filter(({ inner }) => inner.startsWith('ui/') !== inner.endsWith('.vue'))
      .map(({ path }) => path)

    expect(misplaced, 'A slice ui segment holds its .vue components and nothing else').toEqual([])
  })

  it('expose public APIs that only re-export their modules', () => {
    const modules = sourceFiles.filter(({ path }) => /\.(?:ts|vue)$/.test(path))
    const publicApis = modules.filter(({ path }) => {
      if (path.includes('/@x/')) return true
      if (!path.endsWith('/index.ts') || path.startsWith('src/app/')) return false
      const folder = path.slice(0, -'index.ts'.length)
      return modules.some((module) => module.path !== path && module.path.startsWith(folder))
    })
    expect(publicApis).not.toHaveLength(0)

    const implementing = publicApis
      .filter((file) => scriptsOf(file).some(({ statements }) => !statements.every(isReExport)))
      .map(({ path }) => path)

    expect(
      implementing,
      'An index.ts that fronts other modules, and every @x file, only re-exports them',
    ).toEqual([])
  })

  it('cross-import only through the reviewed @x public APIs', () => {
    const crossImports = sourceFiles.map(({ path }) => path).filter((path) => path.includes('/@x/'))

    expect(
      crossImports,
      'Review a new @x cross-import and list it in ARCHITECTURE.md before adding it here',
    ).toEqual(['src/entities/user/@x/account.ts'])
  })
})
