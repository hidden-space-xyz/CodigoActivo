import { generateHTML } from '@tiptap/core'
import type { AnyExtension, JSONContent } from '@tiptap/core'
import { Color } from '@tiptap/extension-color'
import Highlight from '@tiptap/extension-highlight'
import Image from '@tiptap/extension-image'
import Link from '@tiptap/extension-link'
import { Table } from '@tiptap/extension-table'
import { TableCell } from '@tiptap/extension-table-cell'
import { TableHeader } from '@tiptap/extension-table-header'
import { TableRow } from '@tiptap/extension-table-row'
import TextAlign from '@tiptap/extension-text-align'
import { TextStyle } from '@tiptap/extension-text-style'
import Underline from '@tiptap/extension-underline'
import StarterKit from '@tiptap/starter-kit'

const EMPTY_DOC: JSONContent = { type: 'doc', content: [] }
export const EMPTY_DOC_JSON = JSON.stringify(EMPTY_DOC)

const FILE_CONTENT_URL =
  /^\/api\/files\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\/content$/i
const SAFE_COLOR = /^#[0-9a-f]{6}$/i
const SAFE_ALIGNMENTS = new Set(['left', 'center', 'right'])
const SAFE_NODE_TYPES = new Set([
  'doc',
  'paragraph',
  'text',
  'heading',
  'blockquote',
  'bulletList',
  'orderedList',
  'listItem',
  'codeBlock',
  'hardBreak',
  'horizontalRule',
  'image',
  'table',
  'tableRow',
  'tableHeader',
  'tableCell',
])
const SAFE_MARK_TYPES = new Set([
  'bold',
  'italic',
  'strike',
  'code',
  'underline',
  'link',
  'textStyle',
  'highlight',
])
const MAX_RICH_TEXT_NODES = 5_000
const MAX_RICH_TEXT_CHARACTERS = 500_000
const MAX_RICH_TEXT_DEPTH = 50

function isSameOriginImageSrc(src: string): boolean {
  if (src.startsWith('/') && !src.startsWith('//')) return true
  try {
    return new URL(src, window.location.origin).origin === window.location.origin
  } catch {
    return false
  }
}

const SameOriginImage = Image.extend({
  parseHTML() {
    return [
      {
        tag: 'img[src]',
        getAttrs: (element) => {
          if (!(element instanceof HTMLElement)) return false
          return isSameOriginImageSrc(element.getAttribute('src') ?? '') ? null : false
        },
      },
    ]
  },
})

export function richTextExtensions(): AnyExtension[] {
  return [
    StarterKit.configure({ link: false, underline: false }),
    TextStyle,
    Color,
    Highlight.configure({ multicolor: true }),
    Underline,
    Link.configure({
      openOnClick: false,
      autolink: true,
      HTMLAttributes: { rel: 'noopener nofollow', target: '_blank' },
    }),
    TextAlign.configure({ types: ['heading', 'paragraph', 'image'] }),
    Table.configure({ resizable: true }),
    TableRow,
    TableHeader,
    TableCell,
    SameOriginImage,
  ]
}

let rendererExtensions: AnyExtension[] | undefined

export function renderRichTextHtml(value?: string | null): string {
  rendererExtensions ??= richTextExtensions()
  try {
    return generateHTML(parseRichText(value), rendererExtensions)
  } catch {
    return ''
  }
}

export function parseRichText(value?: string | null): JSONContent {
  if (!value) return { type: 'doc', content: [] }
  try {
    const parsed: unknown = JSON.parse(value)
    if (parsed && typeof parsed === 'object' && (parsed as JSONContent).type === 'doc') {
      return sanitizeRichText(parsed)
    }
    return { type: 'doc', content: [] }
  } catch {
    return {
      type: 'doc',
      content: [
        {
          type: 'paragraph',
          content: [{ type: 'text', text: value.slice(0, MAX_RICH_TEXT_CHARACTERS) }],
        },
      ],
    }
  }
}

interface SanitizeState {
  nodes: number
  characters: number
}

function sanitizeRichText(value: JSONContent): JSONContent {
  const state: SanitizeState = { nodes: 0, characters: 0 }
  return sanitizeNode(value, state, 0) ?? { ...EMPTY_DOC }
}

function sanitizeNode(value: JSONContent, state: SanitizeState, depth: number): JSONContent | null {
  if (
    !value ||
    typeof value !== 'object' ||
    typeof value.type !== 'string' ||
    !SAFE_NODE_TYPES.has(value.type) ||
    depth > MAX_RICH_TEXT_DEPTH ||
    state.nodes >= MAX_RICH_TEXT_NODES
  ) {
    return null
  }

  state.nodes += 1
  const clean: JSONContent = { type: value.type }

  if (value.type === 'text') {
    if (typeof value.text !== 'string') return null
    const available = Math.max(0, MAX_RICH_TEXT_CHARACTERS - state.characters)
    clean.text = value.text.slice(0, available)
    state.characters += clean.text.length
    const marks = (value.marks ?? [])
      .map(sanitizeMark)
      .filter((mark): mark is NonNullable<JSONContent['marks']>[number] => mark !== null)
    if (marks.length > 0) clean.marks = marks
    return clean
  }

  const attrs = sanitizeNodeAttrs(value.type, value.attrs)
  if (attrs) clean.attrs = attrs

  const content = (value.content ?? [])
    .map((child) => sanitizeNode(child, state, depth + 1))
    .filter((child): child is JSONContent => child !== null)
  if (content.length > 0) clean.content = content
  return clean
}

function sanitizeNodeAttrs(
  type: string,
  attrs: Record<string, unknown> | undefined,
): Record<string, unknown> | undefined {
  if (!attrs) return undefined
  const clean: Record<string, unknown> = {}

  if (
    (type === 'paragraph' || type === 'heading' || type === 'image') &&
    typeof attrs.textAlign === 'string' &&
    SAFE_ALIGNMENTS.has(attrs.textAlign)
  ) {
    clean.textAlign = attrs.textAlign
  }

  if (type === 'heading' && Number.isInteger(attrs.level)) {
    clean.level = Math.min(6, Math.max(1, Number(attrs.level)))
  }

  if (type === 'orderedList' && Number.isInteger(attrs.start)) {
    clean.start = Math.min(100_000, Math.max(1, Number(attrs.start)))
  }

  if (type === 'codeBlock' && typeof attrs.language === 'string') {
    const language = attrs.language.trim()
    if (/^[a-z0-9_+.-]{1,40}$/i.test(language)) clean.language = language
  }

  if (type === 'image' && typeof attrs.src === 'string' && FILE_CONTENT_URL.test(attrs.src)) {
    clean.src = attrs.src
    if (typeof attrs.alt === 'string') clean.alt = attrs.alt.slice(0, 300)
    if (typeof attrs.title === 'string') clean.title = attrs.title.slice(0, 300)
  }

  if (type === 'tableCell' || type === 'tableHeader') {
    const colspan = boundedInteger(attrs.colspan, 1, 100)
    const rowspan = boundedInteger(attrs.rowspan, 1, 100)
    if (colspan !== undefined) clean.colspan = colspan
    if (rowspan !== undefined) clean.rowspan = rowspan
    if (Array.isArray(attrs.colwidth)) {
      clean.colwidth = attrs.colwidth
        .slice(0, colspan ?? 1)
        .map((width) => boundedInteger(width, 10, 2_000))
        .filter((width): width is number => width !== undefined)
    }
  }

  return Object.keys(clean).length > 0 ? clean : undefined
}

function sanitizeMark(
  mark: NonNullable<JSONContent['marks']>[number],
): NonNullable<JSONContent['marks']>[number] | null {
  if (!mark || typeof mark.type !== 'string' || !SAFE_MARK_TYPES.has(mark.type)) return null
  if (mark.type === 'link') {
    const href = safeLink(mark.attrs?.href)
    return href
      ? { type: 'link', attrs: { href, target: '_blank', rel: 'noopener noreferrer nofollow' } }
      : null
  }
  if (mark.type === 'textStyle' || mark.type === 'highlight') {
    const color = mark.attrs?.color
    return typeof color === 'string' && SAFE_COLOR.test(color)
      ? { type: mark.type, attrs: { color } }
      : null
  }
  return { type: mark.type }
}

function safeLink(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const href = value.trim()
  if (!href || href.startsWith('//')) return null
  try {
    const parsed = new URL(href, window.location.origin)
    if (!['http:', 'https:', 'mailto:', 'tel:'].includes(parsed.protocol)) return null
    if (
      (parsed.protocol === 'http:' || parsed.protocol === 'https:') &&
      (parsed.username || parsed.password)
    )
      return null
    return href
  } catch {
    return null
  }
}

function boundedInteger(value: unknown, minimum: number, maximum: number): number | undefined {
  if (!Number.isInteger(value)) return undefined
  return Math.min(maximum, Math.max(minimum, Number(value)))
}

export function serializeRichText(json: JSONContent): string {
  return JSON.stringify(json)
}

export function isRichTextEmpty(value?: string | null): boolean {
  const doc = parseRichText(value)
  if (!doc.content || doc.content.length === 0) return true
  return doc.content.every(
    (node) => node.type === 'paragraph' && (!node.content || node.content.length === 0),
  )
}

export function isRichTextBlank(value?: string | null): boolean {
  return !hasRichTextContent(parseRichText(value))
}

function hasRichTextContent(node: JSONContent): boolean {
  if (node.type === 'image') return true
  if (typeof node.text === 'string' && node.text.trim().length > 0) return true
  return (node.content ?? []).some(hasRichTextContent)
}

function collectRichTextStrings(node: JSONContent): string[] {
  if (typeof node.text === 'string') return [node.text]
  const parts: string[] = []
  let inline = ''
  for (const child of node.content ?? []) {
    if (typeof child.text === 'string') {
      inline += child.text
      continue
    }
    if (inline) {
      parts.push(inline)
      inline = ''
    }
    parts.push(...collectRichTextStrings(child))
  }
  if (inline) parts.push(inline)
  return parts
}

export function richTextExcerpt(value: string | null | undefined, maxLength = 160): string {
  const text = collectRichTextStrings(parseRichText(value)).join(' ').replace(/\s+/g, ' ').trim()
  if (text.length <= maxLength) return text
  const cut = text.slice(0, maxLength)
  const boundary = cut.lastIndexOf(' ')
  const truncated = boundary > 0 ? cut.slice(0, boundary) : cut
  return `${truncated.trimEnd()}…`
}
