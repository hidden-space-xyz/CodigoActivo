import { describe, expect, it } from 'vitest'

import { listFiles } from './codebase'

describe('test layout', () => {
  it('runs every test file as a *.spec.ts of the unit, integration or architecture project', () => {
    const testFiles = [...listFiles('src'), ...listFiles('tests')].filter(
      (path) => /\.(?:spec|test)\.[cm]?[jt]sx?$/.test(path) || path.includes('/__tests__/'),
    )
    expect(testFiles).not.toHaveLength(0)

    expect(
      testFiles.filter(
        (path) => !/^tests\/(?:unit|integration|architecture)\/.+\.spec\.ts$/.test(path),
      ),
      'Vitest only runs *.spec.ts files under tests/unit, tests/integration and tests/architecture',
    ).toEqual([])
  })
})
