import { describe, expect, it } from 'vitest'

import {
  findDirective,
  sourceFiles,
  staticAttribute,
  templateElements,
  type TemplateElement,
} from './codebase'

const column = 'el-table-column'

const visits = sourceFiles
  .filter(({ path }) => path.endsWith('.vue'))
  .flatMap((file) => templateElements(file).map((visit) => ({ ...visit, path: file.path })))

function location({ path, element }: { path: string; element: TemplateElement }): string {
  return `${path}:${element.loc.start.line}`
}

function isActionsColumn(element: TemplateElement | undefined): boolean {
  return (
    element !== undefined &&
    staticAttribute(element, 'width') === '100' &&
    staticAttribute(element, 'align') === 'right' &&
    staticAttribute(element, 'fixed') === 'right'
  )
}

function hasHeaderSlot(element: TemplateElement): boolean {
  return element.children.some(
    (child) => 'tag' in child && child.tag === 'template' && findDirective(child, 'slot', 'header'),
  )
}

function cellSlots(element: TemplateElement): TemplateElement[] {
  return element.children.filter(
    (child): child is TemplateElement =>
      'tag' in child && child.tag === 'template' && !findDirective(child, 'slot', 'header'),
  )
}

function continuesBranch(element: TemplateElement): boolean {
  return element.props.some(({ name }) => name === 'else-if' || name === 'else')
}

function rootCount(slot: TemplateElement): number {
  let roots = 0
  let inText = false
  for (const child of slot.children) {
    if ('tag' in child) {
      if (!continuesBranch(child)) roots += 1
      inText = false
    } else if (child.loc.source.trim() !== '') {
      if (!inText) roots += 1
      inText = true
    }
  }
  return roots
}

describe('admin tables', () => {
  it('render RowActions in a width="100" align="right" fixed="right" column', () => {
    const rowActions = visits.filter(({ element }) => element.tag === 'RowActions')
    expect(rowActions).not.toHaveLength(0)

    expect(
      rowActions
        .filter(
          ({ ancestors }) => !isActionsColumn(ancestors.findLast(({ tag }) => tag === column)),
        )
        .map(location),
      'RowActions fills its own width="100" align="right" fixed="right" el-table-column',
    ).toEqual([])
  })

  it('label every column that has a #header slot', () => {
    const headed = visits.filter(({ element }) => element.tag === column && hasHeaderSlot(element))
    expect(headed).not.toHaveLength(0)

    expect(
      headed
        .filter(
          ({ element }) =>
            staticAttribute(element, 'label') === undefined &&
            !findDirective(element, 'bind', 'label'),
        )
        .map(location),
      'An el-table-column with a #header slot keeps a label as well',
    ).toEqual([])
  })

  it('render each cell slot as one element, component or text', () => {
    const cells = visits.flatMap(({ path, element }) =>
      element.tag === column ? cellSlots(element).map((slot) => ({ path, element: slot })) : [],
    )
    expect(cells).not.toHaveLength(0)

    expect(
      cells.filter(({ element }) => rootCount(element) !== 1).map(location),
      'A cell slot renders a single root: an element or component (a v-if chain counts once) or text',
    ).toEqual([])
  })
})
