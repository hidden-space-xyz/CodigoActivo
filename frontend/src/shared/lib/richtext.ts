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
  return generateHTML(parseRichText(value), rendererExtensions)
}

export function parseRichText(value?: string | null): JSONContent {
  if (!value) return { type: 'doc', content: [] }
  try {
    const parsed: unknown = JSON.parse(value)
    if (parsed && typeof parsed === 'object' && (parsed as JSONContent).type === 'doc') {
      return parsed as JSONContent
    }
    return { type: 'doc', content: [] }
  } catch {
    return {
      type: 'doc',
      content: [{ type: 'paragraph', content: [{ type: 'text', text: value }] }],
    }
  }
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
