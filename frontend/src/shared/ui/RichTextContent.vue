<script setup lang="ts">
import { computed } from 'vue'

import { renderRichTextHtml } from '@/shared/lib'

const props = defineProps<{ content?: string | null }>()

const html = computed(() => renderRichTextHtml(props.content))
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -->
  <div class="rich-text" v-html="html" />
</template>
