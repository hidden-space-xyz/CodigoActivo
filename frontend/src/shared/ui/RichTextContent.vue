<script setup lang="ts">
import { computed } from 'vue'

import { renderRichTextHtml } from '@/shared/lib'

const props = defineProps<{ content?: string | null }>()

// The HTML comes from generateHTML over admin-authored TipTap JSON — the same DOM the previous
// read-only editor instance produced, without the ProseMirror machinery.
const html = computed(() => renderRichTextHtml(props.content))
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -->
  <div class="rich-text" v-html="html" />
</template>
