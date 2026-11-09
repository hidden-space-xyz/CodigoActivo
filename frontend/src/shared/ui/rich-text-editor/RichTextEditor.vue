<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { JSONContent } from '@tiptap/core'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { useI18n } from 'vue-i18n'

import { AppIcon } from '@/shared/ui/app-icon'
import { fileContentUrl } from '@/shared/api'
import { IMAGE_UPLOAD_ACCEPT, MAX_IMAGE_UPLOAD_BYTES } from '@/shared/config'
import {
  cleanRichText,
  normalizeLink,
  parseRichText,
  richTextExtensions,
  serializeRichText,
} from '@/shared/lib/rich-text'
import { formatFileSize } from '@/shared/lib/number'

const { t } = useI18n()

const props = defineProps<{
  /** Serialized TipTap JSON document; anything else loads as an empty document. */
  modelValue?: string | null
  /** Applies the error border, e.g. when a required description is missing. */
  invalid?: boolean
  /** Accessible name of the editable area; read once at mount. */
  label: string
  /**
   * Uploads an inserted image and resolves to its stored file id, which becomes the image URL.
   * Files over `MAX_IMAGE_UPLOAD_BYTES` are refused before it is called. Resolving `undefined`
   * skips the insert; a rejection shows the upload error message. Without it the editor is text
   * only: it has no image button and drops images from loaded or pasted content. Read once at
   * mount.
   */
  upload?: (file: File) => Promise<string | undefined>
}>()
const emit = defineEmits<{
  /**
   * Fired with the serialized JSON document on every user edit, not on external value updates. It
   * carries only the content the API accepts, as `parseRichText` keeps it.
   */
  'update:modelValue': [value: string]
}>()

const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)
const uploadError = ref('')

const placeholderVar = computed(() => JSON.stringify(t('editor.placeholder')))
const upload = props.upload
const contentOptions = { images: upload !== undefined }

const editor = useEditor({
  extensions: richTextExtensions(contentOptions),
  content: parseRichText(props.modelValue, contentOptions),
  editorProps: {
    attributes: {
      'aria-label': props.label,
      'aria-multiline': 'true',
    },
  },
  onUpdate: ({ editor }) => emit('update:modelValue', currentValue(editor.getJSON())),
})

function currentValue(json: JSONContent): string {
  return serializeRichText(cleanRichText(json, contentOptions))
}

watch(
  () => props.modelValue,
  (value) => {
    const instance = editor.value
    if (!instance) return
    const incoming = serializeRichText(parseRichText(value, contentOptions))
    if (incoming !== currentValue(instance.getJSON())) {
      instance.commands.setContent(parseRichText(value, contentOptions), { emitUpdate: false })
    }
  },
)

function pickImage(): void {
  fileInput.value?.click()
}

async function onFileChange(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file || !editor.value || !upload) return
  if (file.size > MAX_IMAGE_UPLOAD_BYTES) {
    uploadError.value = t('common.imageTooLarge', { max: formatFileSize(MAX_IMAGE_UPLOAD_BYTES) })
    return
  }
  uploading.value = true
  uploadError.value = ''
  try {
    const fileId = await upload(file)
    const url = fileContentUrl(fileId)
    if (url) editor.value.chain().focus().setImage({ src: url, alt: '' }).run()
  } catch {
    uploadError.value = t('common.imageUploadFailed')
  } finally {
    uploading.value = false
  }
}

function setColor(value: string): void {
  editor.value?.chain().focus().setColor(value).run()
}

function setHighlight(value: string): void {
  editor.value?.chain().focus().setHighlight({ color: value }).run()
}

function clearColors(): void {
  editor.value?.chain().focus().unsetColor().unsetHighlight().run()
}

function setAlign(value: 'left' | 'center' | 'right'): void {
  editor.value?.chain().focus().setTextAlign(value).run()
}

function toggleLink(): void {
  const instance = editor.value
  if (!instance) return
  const previous = (instance.getAttributes('link').href as string | undefined) ?? ''
  const url = window.prompt(t('editor.linkPrompt'), previous)
  if (url === null) return
  const chain = instance.chain().focus().extendMarkRange('link')
  if (url.trim() === '') {
    chain.unsetLink().run()
    return
  }
  const href = normalizeLink(url)
  if (href) chain.setLink({ href }).run()
  else window.alert(t('editor.linkInvalid'))
}

function insertTable(): void {
  editor.value?.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()
}

/** A command of a toolbar: a button, or a color picker when it has `pick`. */
interface Tool {
  readonly title: string
  readonly run?: () => void
  readonly pick?: (value: string) => void
  readonly icon?: string
  readonly spin?: boolean
  readonly text?: string
  readonly tag?: 'strong' | 'em' | 'u' | 's'
  readonly active?: boolean
  readonly disabled?: boolean
  readonly danger?: boolean
}

/** Groups of the main toolbar, separated by a rule; the last one is pushed to the far end. */
const toolbar = computed<Tool[][]>(() => {
  const instance = editor.value
  if (!instance) return []
  const chain = () => instance.chain().focus()
  const media: Tool[] = [
    {
      title: t('editor.link'),
      icon: 'link',
      active: instance.isActive('link'),
      run: toggleLink,
    },
  ]
  if (upload) {
    media.push({
      title: t('editor.insertImage'),
      icon: uploading.value ? 'spinner' : 'image',
      spin: uploading.value,
      disabled: uploading.value,
      run: pickImage,
    })
  }
  media.push({ title: t('editor.insertTable'), icon: 'table', run: insertTable })
  return [
    [
      {
        title: t('editor.bold'),
        text: 'B',
        tag: 'strong',
        active: instance.isActive('bold'),
        run: () => chain().toggleBold().run(),
      },
      {
        title: t('editor.italic'),
        text: 'I',
        tag: 'em',
        active: instance.isActive('italic'),
        run: () => chain().toggleItalic().run(),
      },
      {
        title: t('editor.underline'),
        text: 'U',
        tag: 'u',
        active: instance.isActive('underline'),
        run: () => chain().toggleUnderline().run(),
      },
      {
        title: t('editor.strike'),
        text: 'S',
        tag: 's',
        active: instance.isActive('strike'),
        run: () => chain().toggleStrike().run(),
      },
    ],
    [
      {
        title: t('editor.heading1'),
        text: 'H1',
        active: instance.isActive('heading', { level: 1 }),
        run: () => chain().toggleHeading({ level: 1 }).run(),
      },
      {
        title: t('editor.heading2'),
        text: 'H2',
        active: instance.isActive('heading', { level: 2 }),
        run: () => chain().toggleHeading({ level: 2 }).run(),
      },
      {
        title: t('editor.subtitle'),
        text: 'H3',
        active: instance.isActive('heading', { level: 3 }),
        run: () => chain().toggleHeading({ level: 3 }).run(),
      },
    ],
    [
      { title: t('editor.textColor'), text: 'A', pick: setColor },
      { title: t('editor.highlight'), icon: 'palette', pick: setHighlight },
      { title: t('editor.clearFormatting'), icon: 'ban', run: clearColors },
    ],
    [
      {
        title: t('editor.alignLeft'),
        icon: 'align-left',
        active: instance.isActive({ textAlign: 'left' }),
        run: () => setAlign('left'),
      },
      {
        title: t('editor.alignCenter'),
        icon: 'align-center',
        active: instance.isActive({ textAlign: 'center' }),
        run: () => setAlign('center'),
      },
      {
        title: t('editor.alignRight'),
        icon: 'align-right',
        active: instance.isActive({ textAlign: 'right' }),
        run: () => setAlign('right'),
      },
    ],
    [
      {
        title: t('editor.bulletList'),
        icon: 'list',
        active: instance.isActive('bulletList'),
        run: () => chain().toggleBulletList().run(),
      },
      {
        title: t('editor.orderedList'),
        text: '1.',
        active: instance.isActive('orderedList'),
        run: () => chain().toggleOrderedList().run(),
      },
      {
        title: t('editor.blockquote'),
        icon: 'comment',
        active: instance.isActive('blockquote'),
        run: () => chain().toggleBlockquote().run(),
      },
    ],
    media,
    [
      {
        title: t('editor.undo'),
        icon: 'undo',
        disabled: !instance.can().undo(),
        run: () => chain().undo().run(),
      },
      {
        title: t('editor.redo'),
        icon: 'refresh',
        disabled: !instance.can().redo(),
        run: () => chain().redo().run(),
      },
    ],
  ]
})

/** Commands of the table the cursor is in, or none outside a table. */
const tableTools = computed<Tool[]>(() => {
  const instance = editor.value
  if (!instance?.isActive('table')) return []
  const chain = () => instance.chain().focus()
  return [
    {
      title: t('editor.addColumn'),
      text: t('editor.columnAdd'),
      run: () => chain().addColumnAfter().run(),
    },
    {
      title: t('editor.deleteColumn'),
      text: t('editor.columnRemove'),
      run: () => chain().deleteColumn().run(),
    },
    { title: t('editor.addRow'), text: t('editor.rowAdd'), run: () => chain().addRowAfter().run() },
    {
      title: t('editor.deleteRow'),
      text: t('editor.rowRemove'),
      run: () => chain().deleteRow().run(),
    },
    {
      title: t('editor.toggleHeaderRow'),
      text: t('editor.headerCell'),
      run: () => chain().toggleHeaderRow().run(),
    },
    {
      title: t('editor.deleteTable'),
      icon: 'trash',
      danger: true,
      run: () => chain().deleteTable().run(),
    },
  ]
})

/** The main toolbar and, while the cursor is in a table, the table toolbar under it. */
const toolbars = computed(() =>
  [
    { table: false, groups: toolbar.value },
    { table: true, groups: [tableTools.value] },
  ].filter(({ groups }) => groups.some((group) => group.length > 0)),
)

onBeforeUnmount(() => editor.value?.destroy())
</script>

<template>
  <div
    class="rt"
    :class="{ 'rt--invalid': invalid }"
    :style="{ '--rt-placeholder': placeholderVar }"
  >
    <div
      v-for="bar in toolbars"
      :key="bar.table ? 'table' : 'main'"
      class="rt__toolbar"
      :class="{ 'rt__toolbar--table': bar.table }"
    >
      <span v-if="bar.table" class="rt__table-label">{{ $t('editor.tableLabel') }}</span>
      <template v-for="(group, index) in bar.groups" :key="index">
        <span
          v-if="index > 0"
          :class="index === bar.groups.length - 1 ? 'rt__spacer' : 'rt__sep'"
        />
        <template v-for="tool in group" :key="tool.title">
          <label v-if="tool.pick" class="rt__btn rt__color" :title="tool.title">
            <span v-if="tool.text" class="rt__color-letter">{{ tool.text }}</span>
            <AppIcon v-else-if="tool.icon" :name="tool.icon" />
            <input
              type="color"
              class="rt__color-input"
              @input="tool.pick?.(($event.target as HTMLInputElement).value)"
            />
          </label>
          <button
            v-else
            type="button"
            class="rt__btn"
            :class="{ 'rt__btn--active': tool.active, 'rt__btn--danger': tool.danger }"
            :title="tool.title"
            :disabled="tool.disabled"
            @click="tool.run?.()"
          >
            <AppIcon v-if="tool.icon" :name="tool.icon" :spin="tool.spin ?? false" />
            <component :is="tool.tag ?? 'span'" v-else>{{ tool.text }}</component>
          </button>
        </template>
      </template>
    </div>

    <EditorContent v-if="editor" :editor="editor" class="rt__content rich-text" />

    <input
      v-if="upload"
      ref="fileInput"
      type="file"
      :accept="IMAGE_UPLOAD_ACCEPT"
      class="rt__file"
      aria-hidden="true"
      tabindex="-1"
      @change="onFileChange"
    />
    <small v-if="uploadError" class="rt__error">{{ uploadError }}</small>
  </div>
</template>

<style scoped>
.rt {
  border: 1px solid var(--ca-border-strong);
  border-radius: 10px;
  background: var(--ca-input-bg);
  overflow: hidden;
}

.rt--invalid {
  border-color: var(--ca-danger);
}

.rt__toolbar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 2px;
  padding: 6px 8px;
  border-bottom: 1px solid var(--ca-border);
  background: var(--ca-surface);
}

.rt__toolbar--table {
  gap: 4px;
  background: var(--ca-bg-elevated);
}

.rt__table-label {
  font-size: 12px;
  color: var(--ca-text-muted);
  margin-right: 4px;
}

.rt__btn {
  position: relative;
  min-width: 30px;
  height: 30px;
  padding: 0 8px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: none;
  border-radius: 7px;
  background: transparent;
  color: var(--ca-text-muted);
  font-size: 14px;
  font-family: var(--ca-font-body);
  cursor: pointer;
  transition:
    background 0.15s ease,
    color 0.15s ease;
}

.rt__btn:disabled {
  opacity: 0.4;
  cursor: default;
}

.rt__btn:hover:not(:disabled) {
  background: var(--ca-bg-elevated);
  color: var(--ca-text-bright);
}

.rt__btn--active {
  background: var(--ca-orange-soft);
  color: var(--ca-orange-ink);
}

.rt__btn--danger:hover:not(:disabled) {
  color: var(--ca-danger-ink);
}

.rt__color {
  overflow: hidden;
}

.rt__color-letter {
  text-decoration: underline;
  text-decoration-color: var(--ca-orange);
  text-underline-offset: 2px;
}

.rt__color-input {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  opacity: 0;
  cursor: pointer;
  border: none;
  padding: 0;
}

.rt__sep {
  width: 1px;
  height: 18px;
  margin: 0 4px;
  background: var(--ca-border-strong);
}

.rt__spacer {
  flex: 1;
}

.rt__content {
  padding: 12px 14px;
}

.rt__content:deep(.ProseMirror) {
  min-height: 160px;
  outline: none;
}

.rt__content:deep(.ProseMirror p.is-editor-empty:first-child::before) {
  content: var(--rt-placeholder);
  color: var(--ca-text-faint);
  float: left;
  height: 0;
  pointer-events: none;
}

.rt__file {
  display: none;
}

.rt__error {
  display: block;
  padding: 0 14px 10px;
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}

@media (pointer: coarse) {
  .rt__btn {
    min-width: 38px;
    height: 38px;
  }
}
</style>
