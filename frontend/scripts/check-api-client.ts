import { mkdtemp, readdir, readFile, rm } from 'node:fs/promises'
import { dirname, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

import { generate } from 'orval'

const frontendRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const apiRoot = join(frontendRoot, 'src', 'shared', 'api')
const generatedRoot = join(apiRoot, 'generated')
const temporaryRoot = await mkdtemp(join(apiRoot, '.generated-check-'))

async function listFiles(root: string, directory = root): Promise<string[]> {
  const entries = await readdir(directory, { withFileTypes: true })
  const files: string[] = []

  for (const entry of entries) {
    const absolutePath = join(directory, entry.name)
    if (entry.isDirectory()) {
      files.push(...(await listFiles(root, absolutePath)))
    } else if (entry.isFile()) {
      files.push(relative(root, absolutePath).split(sep).join('/'))
    }
  }

  return files.sort()
}

async function normalizedContents(root: string, file: string): Promise<string> {
  return (await readFile(join(root, file), 'utf8')).replaceAll('\r\n', '\n')
}

try {
  process.env.ORVAL_GENERATED_ROOT = temporaryRoot
  await generate('./orval.config.ts', frontendRoot, {
    failOnWarnings: true,
    throwOnError: true,
  })

  const expectedFiles = await listFiles(generatedRoot)
  const actualFiles = await listFiles(temporaryRoot)
  const missingFiles = expectedFiles.filter((file) => !actualFiles.includes(file))
  const unexpectedFiles = actualFiles.filter((file) => !expectedFiles.includes(file))
  const changedFiles: string[] = []

  for (const file of expectedFiles.filter((candidate) => actualFiles.includes(candidate))) {
    if (
      (await normalizedContents(generatedRoot, file)) !==
      (await normalizedContents(temporaryRoot, file))
    ) {
      changedFiles.push(file)
    }
  }

  const differences = [
    ...missingFiles.map((file) => `missing: ${file}`),
    ...unexpectedFiles.map((file) => `unexpected: ${file}`),
    ...changedFiles.map((file) => `changed: ${file}`),
  ]

  if (differences.length > 0) {
    throw new Error(
      `Generated API client is out of date. Run "npm run api:generate".\n${differences.join('\n')}`,
    )
  }

  console.log('Generated API client is up to date.')
} finally {
  delete process.env.ORVAL_GENERATED_ROOT
  await rm(temporaryRoot, { recursive: true, force: true })
}
