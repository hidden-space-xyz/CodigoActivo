<script setup lang="ts">
import { computed } from 'vue'

import { renderRichTextHtml } from '@/shared/lib/richtext'

const props = defineProps<{ content?: string | null }>()

const html = computed(() => renderRichTextHtml(props.content))
</script>

<template>
  <div class="rich-text" :innerHTML="html" />
</template>
