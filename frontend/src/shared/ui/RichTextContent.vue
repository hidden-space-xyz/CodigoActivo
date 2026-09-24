<script setup lang="ts">
import { computed } from 'vue'

import { renderRichTextHtml } from '@/shared/lib/richtext'

const props = defineProps<{
  /**
   * Serialized TipTap JSON document, sanitized before rendering to HTML. Empty or unrenderable
   * content outputs nothing.
   */
  content?: string | null
}>()

const html = computed(() => renderRichTextHtml(props.content))
</script>

<template>
  <div class="rich-text" :innerHTML="html" />
</template>
