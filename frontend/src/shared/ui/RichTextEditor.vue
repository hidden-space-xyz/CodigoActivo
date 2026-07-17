<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { useI18n } from 'vue-i18n'

import { postApiFiles } from '@/shared/api/generated/endpoints/files/files'
import { fileContentUrl, parseRichText, richTextExtensions, serializeRichText } from '@/shared/lib'

const { t } = useI18n()

const props = defineProps<{ modelValue?: string | null; invalid?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)
const uploadError = ref('')

const placeholderVar = computed(() => JSON.stringify(t('editor.placeholder')))

const editor = useEditor({
  extensions: richTextExtensions(),
  content: parseRichText(props.modelValue),
  onUpdate: ({ editor }) => emit('update:modelValue', serializeRichText(editor.getJSON())),
})

watch(
  () => props.modelValue,
  (value) => {
    const instance = editor.value
    if (!instance) return
    const incoming = serializeRichText(parseRichText(value))
    if (incoming !== serializeRichText(instance.getJSON())) {
      instance.commands.setContent(parseRichText(value), { emitUpdate: false })
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
  if (!file || !editor.value) return
  uploading.value = true
  uploadError.value = ''
  try {
    const response = await postApiFiles({ file })
    const url = fileContentUrl(response.data.id)
    if (url) editor.value.chain().focus().setImage({ src: url, alt: file.name }).run()
  } catch {
    uploadError.value = t('editor.uploadError')
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
  if (url.trim() === '') chain.unsetLink().run()
  else chain.setLink({ href: url.trim() }).run()
}

function insertTable(): void {
  editor.value?.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()
}

onBeforeUnmount(() => editor.value?.destroy())
</script>

<template>
  <div class="rt" :class="{ 'rt--invalid': invalid }" :style="{ '--rt-placeholder': placeholderVar }">
    <div v-if="editor" class="rt__toolbar">
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('bold') }"
        :title="$t('editor.bold')"
        @click="editor.chain().focus().toggleBold().run()"
      >
        <strong>B</strong>
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('italic') }"
        :title="$t('editor.italic')"
        @click="editor.chain().focus().toggleItalic().run()"
      >
        <em>I</em>
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('underline') }"
        :title="$t('editor.underline')"
        @click="editor.chain().focus().toggleUnderline().run()"
      >
        <span style="text-decoration: underline">U</span>
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('strike') }"
        :title="$t('editor.strike')"
        @click="editor.chain().focus().toggleStrike().run()"
      >
        <s>S</s>
      </button>

      <span class="rt__sep" />

      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('heading', { level: 1 }) }"
        :title="$t('editor.heading1')"
        @click="editor.chain().focus().toggleHeading({ level: 1 }).run()"
      >
        H1
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('heading', { level: 2 }) }"
        :title="$t('editor.heading2')"
        @click="editor.chain().focus().toggleHeading({ level: 2 }).run()"
      >
        H2
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('heading', { level: 3 }) }"
        :title="$t('editor.subtitle')"
        @click="editor.chain().focus().toggleHeading({ level: 3 }).run()"
      >
        H3
      </button>

      <span class="rt__sep" />

      <label class="rt__btn rt__color" :title="$t('editor.textColor')">
        <span class="rt__color-letter">A</span>
        <input
          type="color"
          class="rt__color-input"
          @input="setColor(($event.target as HTMLInputElement).value)"
        />
      </label>
      <label class="rt__btn rt__color" :title="$t('editor.highlight')">
        <i class="pi pi-palette" />
        <input
          type="color"
          class="rt__color-input"
          @input="setHighlight(($event.target as HTMLInputElement).value)"
        />
      </label>
      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.clearFormatting')"
        @click="clearColors"
      >
        <i class="pi pi-ban" />
      </button>

      <span class="rt__sep" />

      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive({ textAlign: 'left' }) }"
        :title="$t('editor.alignLeft')"
        @click="setAlign('left')"
      >
        <i class="pi pi-align-left" />
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive({ textAlign: 'center' }) }"
        :title="$t('editor.alignCenter')"
        @click="setAlign('center')"
      >
        <i class="pi pi-align-center" />
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive({ textAlign: 'right' }) }"
        :title="$t('editor.alignRight')"
        @click="setAlign('right')"
      >
        <i class="pi pi-align-right" />
      </button>

      <span class="rt__sep" />

      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('bulletList') }"
        :title="$t('editor.bulletList')"
        @click="editor.chain().focus().toggleBulletList().run()"
      >
        <i class="pi pi-list" />
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('orderedList') }"
        :title="$t('editor.orderedList')"
        @click="editor.chain().focus().toggleOrderedList().run()"
      >
        1.
      </button>
      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('blockquote') }"
        :title="$t('editor.blockquote')"
        @click="editor.chain().focus().toggleBlockquote().run()"
      >
        <i class="pi pi-comment" />
      </button>

      <span class="rt__sep" />

      <button
        type="button"
        class="rt__btn"
        :class="{ 'rt__btn--active': editor.isActive('link') }"
        :title="$t('editor.link')"
        @click="toggleLink"
      >
        <i class="pi pi-link" />
      </button>
      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.insertImage')"
        :disabled="uploading"
        @click="pickImage"
      >
        <i :class="uploading ? 'pi pi-spin pi-spinner' : 'pi pi-image'" />
      </button>
      <button type="button" class="rt__btn" :title="$t('editor.insertTable')" @click="insertTable">
        <i class="pi pi-table" />
      </button>

      <span class="rt__spacer" />

      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.undo')"
        :disabled="!editor.can().undo()"
        @click="editor.chain().focus().undo().run()"
      >
        <i class="pi pi-undo" />
      </button>
      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.redo')"
        :disabled="!editor.can().redo()"
        @click="editor.chain().focus().redo().run()"
      >
        <i class="pi pi-refresh" />
      </button>
    </div>

    <div v-if="editor && editor.isActive('table')" class="rt__toolbar rt__toolbar--table">
      <span class="rt__table-label">{{ $t('editor.tableLabel') }}</span>
      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.addColumn')"
        @click="editor.chain().focus().addColumnAfter().run()"
      >
        {{ $t('editor.columnAdd') }}
      </button>
      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.deleteColumn')"
        @click="editor.chain().focus().deleteColumn().run()"
      >
        {{ $t('editor.columnRemove') }}
      </button>
      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.addRow')"
        @click="editor.chain().focus().addRowAfter().run()"
      >
        {{ $t('editor.rowAdd') }}
      </button>
      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.deleteRow')"
        @click="editor.chain().focus().deleteRow().run()"
      >
        {{ $t('editor.rowRemove') }}
      </button>
      <button
        type="button"
        class="rt__btn"
        :title="$t('editor.toggleHeaderRow')"
        @click="editor.chain().focus().toggleHeaderRow().run()"
      >
        {{ $t('editor.headerCell') }}
      </button>
      <button
        type="button"
        class="rt__btn rt__btn--danger"
        :title="$t('editor.deleteTable')"
        @click="editor.chain().focus().deleteTable().run()"
      >
        <i class="pi pi-trash" />
      </button>
    </div>

    <EditorContent v-if="editor" :editor="editor" class="rt__content rich-text" />

    <input ref="fileInput" type="file" accept="image/*" class="rt__file" @change="onFileChange" />
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

.rt__btn:disabled {
  opacity: 0.4;
  cursor: default;
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
</style>
