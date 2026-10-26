import { describe, expect, it } from 'vitest'

import {
  captures,
  componentOf,
  sourceFiles,
  staticAttribute,
  templateElements,
  type SourceFile,
} from './codebase'

const themeFiles = ['src/app/styles/variables.css', 'src/app/styles/element-plus-overrides.css']

const declarationPattern = /(--ca-[\w-]+)['"]?\s*:/g
const referencePattern = /(?:var\(\s*|['"`])(--ca-[\w-]+)/g
const colourPattern = /#[\da-f]{3,8}\b|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\(/gi

const styledFiles = sourceFiles.filter(({ path }) => /\.(?:css|vue|ts)$/.test(path))

const declared = new Set(
  styledFiles
    .filter(({ path }) => path.startsWith('src/app/styles/'))
    .flatMap(({ text }) => captures(text, declarationPattern)),
)

function stylesheetsOf(file: SourceFile): string[] {
  if (file.path.endsWith('.css')) return [file.text]
  if (!file.path.endsWith('.vue')) return []
  const inline = templateElements(file).flatMap(
    ({ element }) => staticAttribute(element, 'style') ?? [],
  )
  return [...componentOf(file).styles.map(({ content }) => content), ...inline]
}

describe('theme', () => {
  it('declares the --ca-* custom properties only in app/styles', () => {
    expect(declared.size).toBeGreaterThan(0)

    const local = styledFiles
      .filter(({ path }) => !path.startsWith('src/app/styles/'))
      .flatMap(({ path, text }) =>
        captures(text, declarationPattern).map((name) => `${path}: ${name}`),
      )

    expect(local, 'Theme values are --ca-* variables of app/styles, not component ones').toEqual([])
  })

  it('references only declared --ca-* custom properties', () => {
    const undeclared = styledFiles.flatMap(({ path, text }) =>
      captures(text, referencePattern)
        .filter((name) => !declared.has(name))
        .map((name) => `${path}: ${name}`),
    )

    expect(undeclared, 'Declare the --ca-* variable in app/styles or fix its name').toEqual([])
  })

  it('declares no --ca-* custom property that nothing references', () => {
    const referenced = new Set(styledFiles.flatMap(({ text }) => captures(text, referencePattern)))

    expect(
      [...declared].filter((name) => !referenced.has(name)),
      'Remove unused --ca-* variables from app/styles',
    ).toEqual([])
  })

  it('writes literal colours only in the theme stylesheets', () => {
    const stylesheets = styledFiles
      .filter(({ path }) => !themeFiles.includes(path))
      .flatMap((file) => stylesheetsOf(file).map((stylesheet) => ({ path: file.path, stylesheet })))
    expect(stylesheets).not.toHaveLength(0)

    expect(
      stylesheets.flatMap(({ path, stylesheet }) =>
        captures(stylesheet, colourPattern).map((colour) => `${path}: ${colour}`),
      ),
      'Use a --ca-* variable; only variables.css and element-plus-overrides.css hold colours',
    ).toEqual([])
  })
})
