import { generateJSON, type JSONContent } from '@tiptap/core'
import { describe, expect, it } from 'vitest'
import {
  cleanRichText,
  EMPTY_DOC_JSON,
  isRichTextBlank,
  isRichTextEmpty,
  normalizeLink,
  parseRichText,
  renderRichTextHtml,
  richTextExcerpt,
  richTextExtensions,
  serializeRichText,
} from '@/shared/lib/rich-text'

const FILE_ID = '0f8fad5b-d9cb-469f-a165-70867728950e'
const FILE_URL = `/api/files/${FILE_ID}/content`

function doc(...content: JSONContent[]): string {
  return JSON.stringify({ type: 'doc', content })
}

function paragraph(...content: JSONContent[]): JSONContent {
  return content.length > 0 ? { type: 'paragraph', content } : { type: 'paragraph' }
}

function text(value: string, marks?: JSONContent['marks']): JSONContent {
  return marks ? { type: 'text', text: value, marks } : { type: 'text', text: value }
}

function marksOf(mark: Record<string, unknown>): unknown {
  const parsed = parseRichText(doc(paragraph(text('x', [mark as never]))))
  return parsed.content?.[0]?.content?.[0]?.marks
}

function attrsOf(node: JSONContent): Record<string, unknown> | undefined {
  return parseRichText(doc(node)).content?.[0]?.attrs
}

describe('parseRichText', () => {
  it('returns an empty document for empty input', () => {
    expect(parseRichText(null)).toEqual({ type: 'doc', content: [] })
    expect(parseRichText('')).toEqual({ type: 'doc', content: [] })
    expect(EMPTY_DOC_JSON).toBe('{"type":"doc","content":[]}')
  })

  it('returns an empty document for non-JSON input', () => {
    expect(parseRichText('Just text')).toEqual({ type: 'doc', content: [] })
  })

  it('returns an empty document for JSON that is not a document', () => {
    expect(parseRichText('[1, 2]')).toEqual({ type: 'doc', content: [] })
    expect(parseRichText('null')).toEqual({ type: 'doc', content: [] })
    expect(parseRichText('{"type":"paragraph"}')).toEqual({ type: 'doc', content: [] })
  })

  it('keeps allowed nodes and marks and drops unknown ones', () => {
    const parsed = parseRichText(
      doc(
        paragraph(text('bold', [{ type: 'bold' }, { type: 'script' }])),
        { type: 'iframe', attrs: { src: 'https://evil.test' } },
        { type: 'paragraph', content: [{ type: 'text' }] },
        { type: 'horizontalRule' },
      ),
    )

    expect(parsed).toEqual({
      type: 'doc',
      content: [
        { type: 'paragraph', content: [{ type: 'text', text: 'bold', marks: [{ type: 'bold' }] }] },
        { type: 'paragraph' },
        { type: 'horizontalRule' },
      ],
    })
  })

  it('drops null children and nodes without a string type', () => {
    const parsed = parseRichText(
      JSON.stringify({ type: 'doc', content: [null, { type: 5 }, { type: 'paragraph' }] }),
    )

    expect(parsed).toEqual({ type: 'doc', content: [{ type: 'paragraph' }] })
  })

  it('drops text marks that are not objects with a string type', () => {
    const parsed = parseRichText(doc(paragraph(text('x', [null as never, { type: 1 } as never]))))

    expect(parsed.content?.[0]?.content?.[0]).toEqual({ type: 'text', text: 'x' })
  })

  it('drops nodes nested deeper than the depth limit', () => {
    let node: JSONContent = paragraph(text('deep'))
    for (let level = 0; level < 60; level += 1) node = { type: 'blockquote', content: [node] }

    const serialized = JSON.stringify(parseRichText(doc(node)))

    expect(serialized).not.toContain('deep')
  })

  it('stops keeping nodes after the node limit', () => {
    const paragraphs = Array.from({ length: 6000 }, (_, index) => paragraph(text(`p${index}`)))

    const parsed = parseRichText(doc(...paragraphs))

    // The document itself and each paragraph with its text count as nodes.
    expect(parsed.content).toHaveLength(2500)
  })

  it('truncates text beyond the character limit', () => {
    const long = 'a'.repeat(300_000)

    const parsed = parseRichText(doc(paragraph(text(long)), paragraph(text(long))))

    const lengths = parsed.content?.map((node) => node.content?.[0]?.text?.length)
    expect(lengths).toEqual([300_000, 200_000])
  })

  describe('links', () => {
    it('keeps safe links and forces a new tab with safe rel', () => {
      for (const href of [
        'https://example.test/page',
        'http://example.test',
        'mailto:a@b.test',
        'tel:+34600',
        '/relative',
        '#seccion',
      ]) {
        expect(marksOf({ type: 'link', attrs: { href: ` ${href} ` } })).toEqual([
          { type: 'link', attrs: { href, target: '_blank', rel: 'noopener noreferrer nofollow' } },
        ])
      }
    })

    it('drops unsafe or malformed links', () => {
      for (const attrs of [
        { href: 'javascript:alert(1)' },
        { href: '//evil.test' },
        { href: '/\\evil.test' },
        { href: 'relative/page' },
        { href: 'data:text/html,x' },
        { href: '' },
        { href: 42 },
        { href: 'https://user:pass@example.test' },
        { href: 'http://[' },
        {},
      ]) {
        expect(marksOf({ type: 'link', attrs })).toBeUndefined()
      }
    })

    it('allows credentials only outside http links', () => {
      expect(marksOf({ type: 'link', attrs: { href: 'mailto:user:pass@example.test' } })).toEqual([
        {
          type: 'link',
          attrs: {
            href: 'mailto:user:pass@example.test',
            target: '_blank',
            rel: 'noopener noreferrer nofollow',
          },
        },
      ])
    })
  })

  describe('colors', () => {
    it('keeps six-digit hex colors on text style and highlight marks', () => {
      expect(marksOf({ type: 'textStyle', attrs: { color: '#AABBCC' } })).toEqual([
        { type: 'textStyle', attrs: { color: '#AABBCC' } },
      ])
      expect(marksOf({ type: 'highlight', attrs: { color: '#00ff00' } })).toEqual([
        { type: 'highlight', attrs: { color: '#00ff00' } },
      ])
    })

    it('drops colors that are not six-digit hex values', () => {
      expect(marksOf({ type: 'textStyle', attrs: { color: 'red; background: url(x)' } })).toBe(
        undefined,
      )
      expect(marksOf({ type: 'highlight' })).toBeUndefined()
    })
  })

  describe('node attributes', () => {
    it('keeps safe text alignment on paragraphs, headings and images only', () => {
      expect(attrsOf({ type: 'paragraph', attrs: { textAlign: 'center' } })).toEqual({
        textAlign: 'center',
      })
      expect(attrsOf({ type: 'paragraph', attrs: { textAlign: 'justify' } })).toBeUndefined()
      expect(attrsOf({ type: 'blockquote', attrs: { textAlign: 'left' } })).toBeUndefined()
    })

    it('clamps heading levels and ordered list starts', () => {
      expect(attrsOf({ type: 'heading', attrs: { level: 9, textAlign: 'right' } })).toEqual({
        textAlign: 'right',
        level: 6,
      })
      expect(attrsOf({ type: 'heading', attrs: { level: 0 } })).toEqual({ level: 1 })
      expect(attrsOf({ type: 'heading', attrs: { level: '2' } })).toBeUndefined()
      expect(attrsOf({ type: 'orderedList', attrs: { start: 0 } })).toEqual({ start: 1 })
      expect(attrsOf({ type: 'orderedList', attrs: { start: 1_000_000 } })).toEqual({
        start: 100_000,
      })
    })

    it('keeps only simple code block languages', () => {
      expect(attrsOf({ type: 'codeBlock', attrs: { language: ' c++ ' } })).toEqual({
        language: 'c++',
      })
      expect(attrsOf({ type: 'codeBlock', attrs: { language: 'x" onload="y' } })).toBeUndefined()
    })

    it('keeps images that point to stored files with bounded alt and title', () => {
      const alt = 'a'.repeat(400)

      expect(
        attrsOf({
          type: 'image',
          attrs: { src: FILE_URL, alt, title: 'Title', textAlign: 'left' },
        }),
      ).toEqual({ textAlign: 'left', src: FILE_URL, alt: 'a'.repeat(300), title: 'Title' })
      expect(attrsOf({ type: 'image', attrs: { src: FILE_URL, alt: 5 } })).toEqual({
        src: FILE_URL,
      })
    })

    it('drops images that do not point to a stored file', () => {
      expect(
        parseRichText(doc({ type: 'image', attrs: { src: 'https://evil.test/x.png' } })),
      ).toEqual({ type: 'doc' })
      expect(parseRichText(doc(paragraph(text('a')), { type: 'image' })).content).toHaveLength(1)
    })

    it('bounds table cell spans and column widths', () => {
      const row = (cell: JSONContent): JSONContent => ({
        type: 'table',
        content: [{ type: 'tableRow', content: [cell] }],
      })
      const cellAttrs = (cell: JSONContent) =>
        parseRichText(doc(row(cell))).content?.[0]?.content?.[0]?.content?.[0]?.attrs

      expect(
        cellAttrs({
          type: 'tableCell',
          attrs: { colspan: 2, rowspan: 500, colwidth: [5, 'wide', 3000, 100] },
        }),
      ).toEqual({ colspan: 2, rowspan: 100, colwidth: [10] })
      expect(cellAttrs({ type: 'tableHeader', attrs: { colwidth: [120, 80] } })).toEqual({
        colwidth: [120],
      })
      expect(cellAttrs({ type: 'tableCell', attrs: { colspan: 'x' } })).toBeUndefined()
    })
  })
})

describe('renderRichTextHtml', () => {
  it('renders sanitized HTML with safe links', () => {
    const html = renderRichTextHtml(
      doc(
        { type: 'heading', attrs: { level: 2 }, content: [text('Title')] },
        paragraph(
          text('link', [{ type: 'link', attrs: { href: 'https://example.test' } }]),
          text(' bad', [{ type: 'link', attrs: { href: 'javascript:alert(1)' } }]),
        ),
      ),
    )

    expect(html).toContain('<h2>Title</h2>')
    expect(html).toContain('href="https://example.test"')
    expect(html).toContain('target="_blank"')
    expect(html).not.toContain('javascript')
  })

  it('returns an empty string when the document cannot be rendered', () => {
    expect(renderRichTextHtml(doc(paragraph(text(''))))).toBe('')
  })
})

describe('richTextExtensions image parsing', () => {
  it('accepts pasted images only from the same origin', () => {
    const extensions = richTextExtensions()
    const json = generateJSON(
      [
        `<img src="${FILE_URL}">`,
        `<img src="${window.location.origin}/local.png">`,
        '<img src="https://evil.test/x.png">',
        '<img src="//evil.test/x.png">',
        '<img src="http://[">',
      ].join(''),
      extensions,
    ) as JSONContent

    const sources = JSON.stringify(json).match(/"src":"[^"]*"/g)
    expect(sources).toEqual([`"src":"${FILE_URL}"`, `"src":"${window.location.origin}/local.png"`])
  })
})

describe('text-only rich text', () => {
  const withImage = doc(paragraph(text('Normas')), { type: 'image', attrs: { src: FILE_URL } })

  it('drops images when parsing without images and keeps them otherwise', () => {
    expect(parseRichText(withImage, { images: false })).toEqual({
      type: 'doc',
      content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Normas' }] }],
    })
    expect(parseRichText(withImage).content?.map((node) => node.type)).toEqual([
      'paragraph',
      'image',
    ])
  })

  it('has no image node, so pasted images are dropped', () => {
    const json = generateJSON(
      `<p>Normas</p><img src="${FILE_URL}">`,
      richTextExtensions({ images: false }),
    ) as JSONContent

    expect(JSON.stringify(json)).not.toContain('"image"')
    expect(JSON.stringify(json)).toContain('Normas')
  })
})

describe('serializeRichText', () => {
  it('serializes editor JSON', () => {
    expect(serializeRichText({ type: 'doc', content: [] })).toBe(EMPTY_DOC_JSON)
  })
})

describe('isRichTextEmpty', () => {
  it('is true for documents without nodes or with only empty paragraphs', () => {
    expect(isRichTextEmpty(null)).toBe(true)
    expect(isRichTextEmpty(EMPTY_DOC_JSON)).toBe(true)
    expect(isRichTextEmpty(doc(paragraph(), { type: 'paragraph', content: [] }))).toBe(true)
  })

  it('is false when any node has content, even whitespace', () => {
    expect(isRichTextEmpty(doc(paragraph(text(' '))))).toBe(false)
    expect(isRichTextEmpty(doc({ type: 'horizontalRule' }))).toBe(false)
  })
})

describe('isRichTextBlank', () => {
  it('ignores whitespace-only text', () => {
    expect(isRichTextBlank(doc(paragraph(text('   '))))).toBe(true)
    expect(isRichTextBlank(undefined)).toBe(true)
  })

  it('detects nested text and images', () => {
    expect(
      isRichTextBlank(
        doc({
          type: 'bulletList',
          content: [{ type: 'listItem', content: [paragraph(text('item'))] }],
        }),
      ),
    ).toBe(false)
    expect(isRichTextBlank(doc({ type: 'image', attrs: { src: FILE_URL } }))).toBe(false)
  })
})

describe('richTextExcerpt', () => {
  it('joins block texts with spaces and collapses whitespace', () => {
    const value = doc(
      { type: 'heading', attrs: { level: 1 }, content: [text('Hello')] },
      paragraph(text('big  '), text('world', [{ type: 'bold' }]), { type: 'hardBreak' }, text('!')),
    )

    expect(richTextExcerpt(value)).toBe('Hello big world !')
  })

  it('cuts long text at a word boundary with an ellipsis', () => {
    expect(richTextExcerpt(doc(paragraph(text('one two three four'))), 10)).toBe('one two…')
  })

  it('cuts long text without spaces at the limit', () => {
    expect(richTextExcerpt(doc(paragraph(text('abcdefghijkl'))), 5)).toBe('abcde…')
  })

  it('returns short text unchanged and uses a 160 character default', () => {
    expect(richTextExcerpt(doc(paragraph(text('short'))))).toBe('short')
    expect(richTextExcerpt(doc(paragraph(text('x '.repeat(100)))))).toHaveLength(160)
  })
})

describe('normalizeLink', () => {
  it('adds https to addresses typed without a scheme', () => {
    expect(normalizeLink(' ejemplo.org/eventos ')).toBe('https://ejemplo.org/eventos')
    expect(normalizeLink('ejemplo.org', false)).toBeNull()
  })

  it('keeps web, mail and phone links, root paths and fragments', () => {
    for (const href of ['https://a.test', 'http://a.test/x', 'mailto:a@b.test', 'tel:+34600']) {
      expect(normalizeLink(href)).toBe(href)
    }
    expect(normalizeLink('/eventos')).toBe('/eventos')
    expect(normalizeLink('#arriba')).toBe('#arriba')
  })

  it('refuses other schemes, other hosts and credentials', () => {
    for (const href of [
      '',
      '   ',
      'javascript:alert(1)',
      'ftp://a.test',
      '//evil.test',
      'https://user:pass@a.test',
    ]) {
      expect(normalizeLink(href)).toBeNull()
    }
  })

  it('refuses control characters that browsers drop to reach another host', () => {
    for (const href of [
      '/\t/evil.test',
      '/\n\\evil.test',
      'java\tscript:alert(1)',
      `/eventos${String.fromCharCode(0x7f)}`,
    ]) {
      expect(normalizeLink(href, false)).toBeNull()
    }
  })
})

describe('cleanRichText', () => {
  it('keeps only the allowed content of an editor document', () => {
    expect(
      cleanRichText({
        type: 'doc',
        content: [
          { type: 'paragraph', attrs: { textAlign: null }, content: [text('Hola')] },
          { type: 'image', attrs: { src: FILE_URL } },
        ],
      }),
    ).toEqual({
      type: 'doc',
      content: [
        { type: 'paragraph', content: [text('Hola')] },
        { type: 'image', attrs: { src: FILE_URL } },
      ],
    })
  })

  it('drops images when images are not allowed', () => {
    expect(
      cleanRichText(
        { type: 'doc', content: [{ type: 'image', attrs: { src: FILE_URL } }] },
        { images: false },
      ),
    ).toEqual({ type: 'doc' })
  })
})

describe('richTextExtensions links', () => {
  it('only lets the editor create links to allowed targets', () => {
    const link = richTextExtensions().find((extension) => extension.name === 'link')
    const isAllowedUri = link?.options.isAllowedUri as (
      url: string,
      context: { defaultValidate: (url: string) => boolean },
    ) => boolean
    const context = { defaultValidate: () => true }

    expect(isAllowedUri('https://ejemplo.org', context)).toBe(true)
    expect(isAllowedUri('/eventos', context)).toBe(true)
    expect(isAllowedUri('ftp://ejemplo.org', context)).toBe(false)
    expect(isAllowedUri('https://ejemplo.org', { defaultValidate: () => false })).toBe(false)
  })
})
