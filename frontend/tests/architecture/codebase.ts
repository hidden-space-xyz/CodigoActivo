import { readdirSync, readFileSync } from 'node:fs'
import { join, relative, sep } from 'node:path'

import ts from 'typescript'
import { parse, type SFCDescriptor, type SFCTemplateBlock } from 'vue/compiler-sfc'

type TemplateNode = NonNullable<SFCTemplateBlock['ast']>['children'][number]

export type TemplateElement = Extract<TemplateNode, { tag: string }>

type TemplateProp = TemplateElement['props'][number]

type TemplateDirective = Extract<TemplateProp, { exp: unknown }>

interface TemplateVisit {
  readonly element: TemplateElement
  readonly ancestors: readonly TemplateElement[]
}

export interface SourceFile {
  readonly path: string
  readonly text: string
}

export interface SliceFile extends SourceFile {
  readonly slice: string | undefined
  readonly inner: string
}

interface ModuleImport {
  readonly specifier: string
  readonly dynamic: boolean
}

const frontendRoot = join(import.meta.dirname, '..', '..')

export function listFiles(directory: string): string[] {
  return readdirSync(join(frontendRoot, directory), { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => relative(frontendRoot, join(entry.parentPath, entry.name)).split(sep).join('/'))
    .sort()
}

export const sourceFiles: readonly SourceFile[] = listFiles('src')
  .filter((path) => !path.startsWith('src/shared/api/generated/'))
  .map((path) => ({ path, text: readFileSync(join(frontendRoot, path), 'utf8') }))

const slicedLayerFiles = sourceFiles.filter(({ path }) =>
  /^src\/(?:pages|widgets|features|entities)\//.test(path),
)

export const slices: readonly string[] = slicedLayerFiles
  .map(({ path }) => /^(src\/[^/]+\/.+)\/index\.ts$/.exec(path)?.[1])
  .filter((slice) => slice !== undefined)
  .sort((first, second) => second.length - first.length)

export const sliceFiles: readonly SliceFile[] = slicedLayerFiles.map((file) => {
  const slice = slices.find((root) => file.path.startsWith(`${root}/`))
  return { ...file, slice, inner: slice ? file.path.slice(slice.length + 1) : file.path }
})

export function captures(text: string, pattern: RegExp): string[] {
  return Array.from(text.matchAll(pattern), (match) => match[1] ?? match[0])
}

function cachedByPath<T>(compute: (file: SourceFile) => T): (file: SourceFile) => T {
  const cache = new Map<string, T>()
  return (file) => {
    const cached = cache.get(file.path)
    if (cached !== undefined) return cached
    const computed = compute(file)
    cache.set(file.path, computed)
    return computed
  }
}

export const componentOf = cachedByPath(
  (file): SFCDescriptor => parse(file.text, { filename: file.path, sourceMap: false }).descriptor,
)

function templateExpressions(nodes: readonly TemplateNode[]): string[] {
  return nodes.flatMap((node) => {
    if ('tag' in node) {
      const bound = node.props.flatMap((prop) =>
        'exp' in prop && prop.exp ? [prop.exp.loc.source] : [],
      )
      return [...bound, ...templateExpressions(node.children)]
    }
    return 'content' in node && typeof node.content === 'object' ? [node.content.loc.source] : []
  })
}

export function scriptsOf(file: SourceFile): ts.SourceFile[] {
  const parseScript = (source: string): ts.SourceFile =>
    ts.createSourceFile(file.path, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS)
  if (file.path.endsWith('.ts')) return [parseScript(file.text)]
  if (!file.path.endsWith('.vue')) return []
  const { script, scriptSetup, template } = componentOf(file)
  const blocks = [script, scriptSetup].flatMap((block) => (block ? [block.content] : []))
  return [...blocks, ...templateExpressions(template?.ast?.children ?? [])].map(parseScript)
}

export const nodesOf = cachedByPath((file): readonly ts.Node[] => {
  const nodes: ts.Node[] = []
  const visit = (node: ts.Node): void => {
    nodes.push(node)
    ts.forEachChild(node, visit)
  }
  for (const script of scriptsOf(file)) visit(script)
  return nodes
})

export function importsOf(file: SourceFile): ModuleImport[] {
  return nodesOf(file).flatMap((node): ModuleImport[] => {
    if (ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) {
      const specifier = node.moduleSpecifier
      return specifier && ts.isStringLiteral(specifier)
        ? [{ specifier: specifier.text, dynamic: false }]
        : []
    }
    if (ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword) {
      const [specifier] = node.arguments
      return specifier && ts.isStringLiteral(specifier)
        ? [{ specifier: specifier.text, dynamic: true }]
        : []
    }
    return []
  })
}

function visitElements(
  nodes: readonly TemplateNode[],
  ancestors: readonly TemplateElement[],
): TemplateVisit[] {
  return nodes.flatMap((node) =>
    'tag' in node
      ? [{ element: node, ancestors }, ...visitElements(node.children, [...ancestors, node])]
      : [],
  )
}

export function templateElements(file: SourceFile): TemplateVisit[] {
  return visitElements(componentOf(file).template?.ast?.children ?? [], [])
}

export function staticAttribute(element: TemplateElement, name: string): string | undefined {
  for (const prop of element.props) {
    if (!('exp' in prop) && prop.name === name) return prop.value?.content ?? ''
  }
  return undefined
}

export function findDirective(
  element: TemplateElement,
  name: string,
  argument: string,
): TemplateDirective | undefined {
  return element.props.find(
    (prop): prop is TemplateDirective =>
      'exp' in prop && prop.name === name && prop.arg?.loc.source === argument,
  )
}
