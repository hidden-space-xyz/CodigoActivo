import { describe, expect, it } from 'vitest'

import { findDirective, sourceFiles, templateElements, type TemplateElement } from './codebase'

const CLOSING_OPTIONS = ['close-on-click-modal', 'close-on-press-escape']

const dialogs = sourceFiles
  .filter(({ path }) =>
    /^src\/(?:pages\/admin|features|shared\/ui\/form-dialog)\/.+\.vue$/.test(path),
  )
  .flatMap((file) =>
    templateElements(file)
      .filter(({ element }) => element.tag === 'el-dialog')
      .map(({ element }) => ({ path: file.path, element })),
  )

function holdsForm(element: TemplateElement): boolean {
  return element.children.some(
    (child) => 'tag' in child && (child.tag === 'form' || holdsForm(child)),
  )
}

function closesOnlyThroughButtons(element: TemplateElement): boolean {
  return CLOSING_OPTIONS.every(
    (option) => findDirective(element, 'bind', option)?.exp?.loc.source === 'false',
  )
}

describe('admin dialogs', () => {
  it('holding a form close only through their buttons, so a stray click or Escape keeps the draft', () => {
    const forms = dialogs.filter(({ element }) => holdsForm(element))
    expect(forms).not.toHaveLength(0)

    expect(
      forms
        .filter(({ element }) => !closesOnlyThroughButtons(element))
        .map(({ path, element }) => `${path}:${element.loc.start.line}`),
      'An admin dialog with a <form> sets :close-on-click-modal="false" and :close-on-press-escape="false"',
    ).toEqual([])
  })
})
