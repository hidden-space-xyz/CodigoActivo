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

// The CSP served by nginx only allows same-origin images (img-src 'self'),
// so images pasted from other sites would save fine but render broken for
// every visitor. Rejecting them at parse time keeps pasted content honest;
// the toolbar's upload flow inserts same-origin URLs via setImage and is
// unaffected.
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
