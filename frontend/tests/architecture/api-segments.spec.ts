import { describe, expect, it } from 'vitest'

import { importsOf, sliceFiles, type SliceFile } from './codebase'

const apiFiles = sliceFiles.filter(({ inner }) => inner.startsWith('api/'))

function importers(matches: (specifier: string) => boolean): SliceFile[] {
  return apiFiles.filter((file) => importsOf(file).some(({ specifier }) => matches(specifier)))
}

function outside(files: readonly SliceFile[], modules: readonly string[]): string[] {
  return files.filter(({ inner }) => !modules.includes(inner)).map(({ path }) => path)
}

describe('api segments', () => {
  it('split server access into requests.ts, mapper.ts, queries.ts and mutations.ts', () => {
    expect(apiFiles).not.toHaveLength(0)

    expect(
      outside(apiFiles, ['api/requests.ts', 'api/mapper.ts', 'api/queries.ts', 'api/mutations.ts']),
      'An api segment holds only requests.ts, mapper.ts, queries.ts and mutations.ts',
    ).toEqual([])
  })

  it('call the generated endpoints only from requests.ts and mapper.ts', () => {
    const endpoints = importers((specifier) =>
      specifier.startsWith('@/shared/api/generated/endpoints/'),
    )
    expect(endpoints).not.toHaveLength(0)

    expect(
      outside(endpoints, ['api/requests.ts', 'api/mapper.ts']),
      'Only requests.ts calls the generated client; mapper.ts may build its URLs',
    ).toEqual([])
  })

  it('handle the generated DTOs only in mapper.ts', () => {
    const models = importers((specifier) => specifier.startsWith('@/shared/api/generated/models'))
    expect(models).not.toHaveLength(0)

    expect(
      outside(models, ['api/mapper.ts']),
      'Only mapper.ts imports the generated models; requests.ts converts through it',
    ).toEqual([])
  })

  it('map the DTOs into the model/types.ts of their slice', () => {
    const mappers = apiFiles.filter(({ inner }) => inner === 'api/mapper.ts')
    const typed = importers((specifier) => specifier === '../model/types')
    expect(mappers).not.toHaveLength(0)

    expect(
      mappers.filter((mapper) => !typed.includes(mapper)).map(({ path }) => path),
      'Each mapper.ts turns DTOs into the types of its slice model/types.ts',
    ).toEqual([])
  })
})
