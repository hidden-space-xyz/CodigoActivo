import { describe, expect, it } from 'vitest'

import {
  captures,
  componentOf,
  findDirective,
  sourceFiles,
  staticAttribute,
  templateElements,
} from './codebase'

const selectorPattern = /\.(ca-[\w-]+)/g
const classPattern = /(?<![\w-])(ca-[\w-]+)/g

const components = sourceFiles.filter(({ path }) => path.endsWith('.vue'))

const globalClasses = new Set(
  sourceFiles
    .filter(({ path }) => path.startsWith('src/app/styles/'))
    .flatMap(({ text }) => captures(text, selectorPattern)),
)

describe('global ca-* classes', () => {
  it('are styled only by app/styles', () => {
    const styles = components.flatMap((file) =>
      componentOf(file).styles.map(({ content }) => ({ path: file.path, content })),
    )
    expect(styles).not.toHaveLength(0)

    expect(
      styles.flatMap(({ path, content }) =>
        captures(content, selectorPattern).map((name) => `${path}: .${name}`),
      ),
      'Use the global ca-* classes as they are instead of scoped copies',
    ).toEqual([])
  })

  it('are used in templates only when app/styles defines them', () => {
    const used = components.flatMap((file) =>
      templateElements(file).flatMap(({ element }) =>
        [
          staticAttribute(element, 'class'),
          findDirective(element, 'bind', 'class')?.exp?.loc.source,
        ]
          .flatMap((classes) => (classes ? captures(classes, classPattern) : []))
          .map((name) => ({ path: file.path, name })),
      ),
    )
    expect(used).not.toHaveLength(0)

    expect(
      used
        .filter(({ name }) => !globalClasses.has(name))
        .map(({ path, name }) => `${path}: ${name}`),
      'Every ca-* class a template uses is defined in app/styles',
    ).toEqual([])
  })
})
