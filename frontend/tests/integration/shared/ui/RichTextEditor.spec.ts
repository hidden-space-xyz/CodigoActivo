import type { Editor, JSONContent } from '@tiptap/core'
import { EditorContent } from '@tiptap/vue-3'
import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import RichTextEditor from '@/shared/ui/RichTextEditor.vue'

import { renderWithProviders, t } from '../../../support/render'

const FILE_ID = '0f8fad5b-d9cb-469f-a165-70867728950e'

function doc(...content: JSONContent[]): string {
  return JSON.stringify({ type: 'doc', content })
}

function paragraph(text: string): JSONContent {
  return { type: 'paragraph', content: [{ type: 'text', text }] }
}

type Upload = (file: File) => Promise<string | undefined>

async function renderEditor(
  props: { modelValue?: string | null; invalid?: boolean; upload?: Upload } = {},
) {
  const rendered = await renderWithProviders(RichTextEditor, {
    props: {
      label: 'Description',
      upload: vi.fn<Upload>(() => Promise.resolve(undefined)),
      modelValue: doc(paragraph('Hello world')),
      ...props,
    },
    attach: true,
  })
  const editor = rendered.wrapper.findComponent(EditorContent).props('editor') as Editor
  return { ...rendered, editor }
}

function button(wrapper: VueWrapper, title: string) {
  const match = wrapper
    .findAll('button')
    .find((candidate) => candidate.attributes('title') === title)
  if (!match) throw new Error(`Toolbar button "${title}" not found`)
  return match
}

function lastEmitted(wrapper: VueWrapper): JSONContent {
  const events = wrapper.emitted<[string]>('update:modelValue')
  const last = events?.[events.length - 1]
  if (!last) throw new Error('No update was emitted')
  return JSON.parse(last[0]) as JSONContent
}

function firstBlock(wrapper: VueWrapper): JSONContent | undefined {
  return lastEmitted(wrapper).content?.[0]
}

function marks(wrapper: VueWrapper): string[] {
  return (firstBlock(wrapper)?.content?.[0]?.marks ?? []).map((mark) =>
    mark.attrs ? `${mark.type}:${JSON.stringify(mark.attrs)}` : mark.type,
  )
}

/** Waits for the editor's debounced reactive state (two animation frames) to reach the toolbar. */
async function settle(): Promise<void> {
  await flushPromises()
  await new Promise<void>((resolve) => {
    requestAnimationFrame(() => requestAnimationFrame(() => resolve()))
  })
  await flushPromises()
}

async function selectAll(editor: Editor): Promise<void> {
  editor.commands.selectAll()
  await settle()
}

/** Selects the first word of the first paragraph, so block-level buttons report their state. */
async function selectFirstWord(editor: Editor): Promise<void> {
  editor.chain().focus().setTextSelection({ from: 1, to: 6 }).run()
  await settle()
}

describe('RichTextEditor', () => {
  it('loads the initial document into a labelled editable area', async () => {
    const { wrapper } = await renderEditor({ invalid: true })

    const area = wrapper.find('.ProseMirror')
    expect(area.attributes('contenteditable')).toBe('true')
    expect(area.attributes('aria-label')).toBe('Description')
    expect(area.attributes('aria-multiline')).toBe('true')
    expect(area.text()).toBe('Hello world')
    expect(wrapper.find('.rt').classes()).toContain('rt--invalid')
    expect(
      (wrapper.find('.rt').element as HTMLElement).style.getPropertyValue('--rt-placeholder'),
    ).toBe(JSON.stringify(t('editor.placeholder')))
    expect(wrapper.find('.rt__toolbar--table').exists()).toBe(false)
    expect(wrapper.find('.rt__error').exists()).toBe(false)
  })

  it('loads plain text as a paragraph', async () => {
    const { wrapper } = await renderEditor({ modelValue: 'Legacy text' })

    expect(wrapper.find('.ProseMirror p').text()).toBe('Legacy text')
    expect(wrapper.find('.rt').classes()).not.toContain('rt--invalid')
  })

  it('emits the serialized document on user edits', async () => {
    const { wrapper, editor } = await renderEditor()

    editor.chain().focus('end').insertContent('!').run()
    await settle()

    expect(lastEmitted(wrapper)).toEqual({
      type: 'doc',
      content: [
        {
          type: 'paragraph',
          attrs: { textAlign: null },
          content: [{ type: 'text', text: 'Hello world!' }],
        },
      ],
    })
  })

  it('replaces the content on external updates without emitting', async () => {
    const { wrapper } = await renderEditor()

    await wrapper.setProps({ modelValue: doc(paragraph('Replaced')) })
    await settle()

    expect(wrapper.find('.ProseMirror').text()).toBe('Replaced')
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()
  })

  it('keeps the editor state when the incoming value matches the current document', async () => {
    const { wrapper, editor } = await renderEditor()
    editor.chain().focus('end').insertContent('?').run()
    await settle()
    const current = wrapper.emitted<[string]>('update:modelValue')?.at(-1)?.[0]
    const setContent = vi.spyOn(editor.commands, 'setContent')

    await wrapper.setProps({ modelValue: current })

    expect(setContent).not.toHaveBeenCalled()
    expect(wrapper.find('.ProseMirror').text()).toBe('Hello world?')
  })

  it('applies inline formatting from the toolbar and highlights active buttons', async () => {
    const { wrapper, editor } = await renderEditor()
    await selectAll(editor)

    for (const key of ['editor.bold', 'editor.italic', 'editor.underline', 'editor.strike']) {
      await button(wrapper, t(key)).trigger('click')
    }
    await settle()

    expect(marks(wrapper).sort()).toEqual(['bold', 'italic', 'strike', 'underline'])
    for (const key of ['editor.bold', 'editor.italic', 'editor.underline', 'editor.strike']) {
      expect(button(wrapper, t(key)).classes()).toContain('rt__btn--active')
    }
  })

  it('toggles headings of each level', async () => {
    const { wrapper, editor } = await renderEditor()
    await selectFirstWord(editor)

    for (const [key, level] of [
      ['editor.heading1', 1],
      ['editor.heading2', 2],
      ['editor.subtitle', 3],
    ] as const) {
      await button(wrapper, t(key)).trigger('click')
      await settle()
      expect(firstBlock(wrapper)).toMatchObject({ type: 'heading', attrs: { level } })
      expect(button(wrapper, t(key)).classes()).toContain('rt__btn--active')
    }

    await button(wrapper, t('editor.subtitle')).trigger('click')
    await settle()
    expect(firstBlock(wrapper)?.type).toBe('paragraph')
  })

  it('sets text and highlight colors and clears them', async () => {
    const { wrapper, editor } = await renderEditor()
    await selectAll(editor)
    const [textColor, highlight] = wrapper.findAll('input[type="color"]')

    await textColor?.setValue('#ff0000')
    await highlight?.setValue('#00ff00')
    await settle()

    expect(marks(wrapper)).toEqual([
      'textStyle:{"color":"#ff0000"}',
      'highlight:{"color":"#00ff00"}',
    ])

    await selectAll(editor)
    await button(wrapper, t('editor.clearFormatting')).trigger('click')
    await settle()

    expect(marks(wrapper)).toEqual([])
  })

  it('aligns paragraphs', async () => {
    const { wrapper, editor } = await renderEditor()
    await selectAll(editor)

    for (const [key, align] of [
      ['editor.alignCenter', 'center'],
      ['editor.alignRight', 'right'],
      ['editor.alignLeft', 'left'],
    ] as const) {
      await button(wrapper, t(key)).trigger('click')
      await settle()
      expect(firstBlock(wrapper)?.attrs).toEqual({ textAlign: align })
      expect(button(wrapper, t(key)).classes()).toContain('rt__btn--active')
    }
  })

  it('wraps content in lists and quotes', async () => {
    const { wrapper, editor } = await renderEditor()

    for (const [key, type] of [
      ['editor.bulletList', 'bulletList'],
      ['editor.orderedList', 'orderedList'],
      ['editor.blockquote', 'blockquote'],
    ] as const) {
      await selectFirstWord(editor)
      await button(wrapper, t(key)).trigger('click')
      await settle()
      expect(firstBlock(wrapper)?.type).toBe(type)
      expect(button(wrapper, t(key)).classes()).toContain('rt__btn--active')
      await button(wrapper, t(key)).trigger('click')
      await settle()
      expect(firstBlock(wrapper)?.type).toBe('paragraph')
    }
  })

  it('adds, edits and removes links through a prompt', async () => {
    const prompt = vi.spyOn(window, 'prompt')
    const { wrapper, editor } = await renderEditor()
    await selectAll(editor)
    const link = button(wrapper, t('editor.link'))

    prompt.mockReturnValueOnce(' https://example.test ')
    await link.trigger('click')
    await settle()

    expect(prompt).toHaveBeenLastCalledWith(t('editor.linkPrompt'), '')
    expect(firstBlock(wrapper)?.content?.[0]?.marks?.[0]).toMatchObject({
      type: 'link',
      attrs: { href: 'https://example.test' },
    })
    expect(link.classes()).toContain('rt__btn--active')

    prompt.mockReturnValueOnce(null)
    const updates = wrapper.emitted('update:modelValue')?.length
    await link.trigger('click')
    expect(prompt).toHaveBeenLastCalledWith(t('editor.linkPrompt'), 'https://example.test')
    expect(wrapper.emitted('update:modelValue')?.length).toBe(updates)

    prompt.mockReturnValueOnce('   ')
    await link.trigger('click')
    await settle()

    expect(marks(wrapper)).toEqual([])
  })

  it('inserts a table and edits it from the table toolbar', async () => {
    const { wrapper } = await renderEditor({ modelValue: null })

    await button(wrapper, t('editor.insertTable')).trigger('click')
    await settle()

    const table = () => lastEmitted(wrapper).content?.find((node) => node.type === 'table')
    const size = () => ({
      rows: table()?.content?.length,
      cols: table()?.content?.[0]?.content?.length,
    })
    expect(size()).toEqual({ rows: 3, cols: 3 })
    expect(table()?.content?.[0]?.content?.[0]?.type).toBe('tableHeader')
    expect(wrapper.find('.rt__toolbar--table').text()).toContain(t('editor.tableLabel'))

    await button(wrapper, t('editor.toggleHeaderRow')).trigger('click')
    await settle()
    expect(table()?.content?.[0]?.content?.[0]?.type).toBe('tableCell')

    await button(wrapper, t('editor.addColumn')).trigger('click')
    await settle()
    expect(size()).toEqual({ rows: 3, cols: 4 })

    await button(wrapper, t('editor.deleteColumn')).trigger('click')
    await settle()
    expect(size()).toEqual({ rows: 3, cols: 3 })

    await button(wrapper, t('editor.addRow')).trigger('click')
    await settle()
    expect(size()).toEqual({ rows: 4, cols: 3 })

    await button(wrapper, t('editor.deleteRow')).trigger('click')
    await settle()
    expect(size()).toEqual({ rows: 3, cols: 3 })

    await button(wrapper, t('editor.deleteTable')).trigger('click')
    await settle()
    expect(table()).toBeUndefined()
    expect(wrapper.find('.rt__toolbar--table').exists()).toBe(false)
  })

  it('undoes and redoes edits', async () => {
    const { wrapper, editor } = await renderEditor()
    const undo = () => button(wrapper, t('editor.undo'))
    const redo = () => button(wrapper, t('editor.redo'))
    expect(undo().attributes('disabled')).toBeDefined()
    expect(redo().attributes('disabled')).toBeDefined()

    editor.chain().focus('end').insertContent(' again').run()
    await settle()
    expect(undo().attributes('disabled')).toBeUndefined()

    await undo().trigger('click')
    await settle()
    expect(wrapper.find('.ProseMirror').text()).toBe('Hello world')
    expect(redo().attributes('disabled')).toBeUndefined()

    await redo().trigger('click')
    await settle()
    expect(wrapper.find('.ProseMirror').text()).toBe('Hello world again')
  })

  describe('image upload', () => {
    function chooseFile(wrapper: VueWrapper, files: File[]): HTMLInputElement {
      const input = wrapper.find<HTMLInputElement>('input[type="file"]').element
      Object.defineProperty(input, 'files', { value: files, configurable: true })
      input.dispatchEvent(new Event('change'))
      return input
    }

    it('opens the file picker from the toolbar', async () => {
      const click = vi
        .spyOn(HTMLInputElement.prototype, 'click')
        .mockImplementation(() => undefined)
      const { wrapper } = await renderEditor()

      await button(wrapper, t('editor.insertImage')).trigger('click')

      expect(click).toHaveBeenCalledTimes(1)
      expect(wrapper.find('input[type="file"]').attributes('accept')).toBe('image/*')
    })

    it('uploads the chosen image and inserts it with the stored file URL', async () => {
      let resolveUpload: (id: string | undefined) => void = () => undefined
      const upload = vi.fn<Upload>(
        () =>
          new Promise((resolve) => {
            resolveUpload = resolve
          }),
      )
      const { wrapper } = await renderEditor({ upload })
      const file = new File(['png'], 'poster.png', { type: 'image/png' })

      chooseFile(wrapper, [file])
      await settle()

      expect(upload).toHaveBeenCalledWith(file)
      const insert = button(wrapper, t('editor.insertImage'))
      expect(insert.attributes('disabled')).toBeDefined()
      expect(insert.find('.app-icon--spin').exists()).toBe(true)

      resolveUpload(FILE_ID)
      await settle()

      expect(insert.attributes('disabled')).toBeUndefined()
      const image = lastEmitted(wrapper).content?.find((node) => node.type === 'image')
      expect(image?.attrs).toMatchObject({
        src: `/api/files/${FILE_ID}/content`,
        alt: 'poster.png',
      })
      expect(wrapper.find('.rt__error').exists()).toBe(false)
    })

    it('skips the insert when the upload returns no id', async () => {
      const upload = vi.fn<Upload>(() => Promise.resolve(undefined))
      const { wrapper } = await renderEditor({ upload })

      chooseFile(wrapper, [new File(['png'], 'x.png', { type: 'image/png' })])
      await settle()

      expect(upload).toHaveBeenCalledTimes(1)
      expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    })

    it('shows an error when the upload fails', async () => {
      const upload = vi.fn<Upload>(() => Promise.reject(new Error('Too large')))
      const { wrapper } = await renderEditor({ upload })

      chooseFile(wrapper, [new File(['png'], 'x.png', { type: 'image/png' })])
      await settle()

      expect(wrapper.find('.rt__error').text()).toBe(t('editor.uploadError'))
      expect(button(wrapper, t('editor.insertImage')).attributes('disabled')).toBeUndefined()
    })

    it('does nothing when no file was chosen', async () => {
      const upload = vi.fn<Upload>()
      const { wrapper } = await renderEditor({ upload })

      chooseFile(wrapper, [])
      await settle()

      expect(upload).not.toHaveBeenCalled()
    })
  })

  it('destroys the editor when unmounted', async () => {
    const { wrapper, editor } = await renderEditor()

    wrapper.unmount()

    expect(editor.isDestroyed).toBe(true)
  })
})
