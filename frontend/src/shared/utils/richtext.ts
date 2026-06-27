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

export type { JSONContent }

/** An empty TipTap document — the canonical "no content" value for the jsonb column. */
export const EMPTY_DOC: JSONContent = { type: 'doc', content: [] }
export const EMPTY_DOC_JSON = JSON.stringify(EMPTY_DOC)

/**
 * Shared extension set used by both the editor and the read-only renderer.
 * Must stay identical on both sides so stored documents render the same way.
 */
export function richTextExtensions(): AnyExtension[] {
  return [
    StarterKit,
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
    Image,
  ]
}

/** Parse the stored jsonb string into a TipTap document, tolerating legacy/empty values. */
export function parseRichText(value?: string | null): JSONContent {
  if (!value) return { type: 'doc', content: [] }
  try {
    const parsed: unknown = JSON.parse(value)
    if (parsed && typeof parsed === 'object' && (parsed as JSONContent).type === 'doc') {
      return parsed as JSONContent
    }
    return { type: 'doc', content: [] }
  } catch {
    // Legacy plain-text content stored before the rich editor existed.
    return {
      type: 'doc',
      content: [{ type: 'paragraph', content: [{ type: 'text', text: value }] }],
    }
  }
}

export function serializeRichText(json: JSONContent): string {
  return JSON.stringify(json)
}

/** True when the document has no meaningful content (empty or a single blank paragraph). */
export function isRichTextEmpty(value?: string | null): boolean {
  const doc = parseRichText(value)
  if (!doc.content || doc.content.length === 0) return true
  return doc.content.every(
    (node) => node.type === 'paragraph' && (!node.content || node.content.length === 0),
  )
}
