import { describe, expect, it } from 'vitest'

import packageJson from '../../package.json'

import { importsOf, sourceFiles } from './codebase'

const runtimeDependencies = new Set(Object.keys(packageJson.dependencies))

const packageImports = sourceFiles.flatMap((file) =>
  importsOf(file).flatMap(({ specifier }) => {
    const name = /^(@[^/]+\/[^/]+|[^@./][^/]*)/.exec(specifier)?.[1]
    return name ? [{ path: file.path, name }] : []
  }),
)

describe('dependencies', () => {
  it('give the application only packages declared as runtime dependencies', () => {
    expect(packageImports).not.toHaveLength(0)

    expect(
      packageImports
        .filter(({ name }) => !runtimeDependencies.has(name))
        .map(({ path, name }) => `${path}: ${name}`),
      'src imports only packages listed in package.json dependencies',
    ).toEqual([])
  })

  it('leave state to TanStack Query instead of a store library', () => {
    const declared = Object.keys({ ...packageJson.dependencies, ...packageJson.devDependencies })
    expect(declared).toContain('@tanstack/vue-query')

    expect(
      declared.filter((name) => ['pinia', 'vuex'].includes(name)),
      'The session and server state live in the TanStack Query cache; Pinia is not used',
    ).toEqual([])
  })
})
