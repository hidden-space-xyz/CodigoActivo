import { describe, expect, it } from 'vitest'

import { importsOf, slices, sourceFiles } from './codebase'

const routeTable = 'src/app/router/routes.ts'

const pageImports = sourceFiles.flatMap((file) =>
  importsOf(file)
    .filter(({ specifier }) => specifier === '@/pages' || specifier.startsWith('@/pages/'))
    .map((pageImport) => ({ ...pageImport, path: file.path })),
)

describe('pages', () => {
  it('are each loaded lazily by the route table through their public API', () => {
    const pages = slices.filter((slice) => slice.startsWith('src/pages/'))
    const routed = pageImports.filter(({ path, dynamic }) => path === routeTable && dynamic)
    expect(pages).not.toHaveLength(0)

    expect(
      [...new Set(routed.map(({ specifier }) => specifier))].sort(),
      'Every page slice, and nothing else, is lazily imported by a route of app/router/routes.ts',
    ).toEqual(pages.map((page) => page.replace(/^src\//, '@/')).sort())
  })

  it('are imported nowhere else', () => {
    expect(pageImports).not.toHaveLength(0)

    expect(
      pageImports
        .filter(({ path, dynamic }) => path !== routeTable || !dynamic)
        .map(({ path, specifier }) => `${path}: ${specifier}`),
      'Only app/router/routes.ts imports pages, and only lazily',
    ).toEqual([])
  })
})
