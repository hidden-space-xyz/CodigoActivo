<script setup lang="ts">
import { onBeforeUnmount, watch } from 'vue'
import { EditorContent, useEditor } from '@tiptap/vue-3'

import { parseRichText, richTextExtensions } from '@/shared/utils/richtext'

const props = defineProps<{ content?: string | null }>()

const editor = useEditor({
  editable: false,
  extensions: richTextExtensions(),
  content: parseRichText(props.content),
})

watch(
  () => props.content,
  (value) => editor.value?.commands.setContent(parseRichText(value), false),
)

onBeforeUnmount(() => editor.value?.destroy())
</script>

<template>
  <EditorContent v-if="editor" :editor="editor" class="rich-text" />
</template>
