<script setup lang="ts">
import { computed, useAttrs } from 'vue'
import Button from 'primevue/button'

defineOptions({ inheritAttrs: false })

const props = defineProps<{ tooltip?: string }>()

const attrs = useAttrs()

const tooltipText = computed(
  () =>
    props.tooltip ??
    (attrs['aria-label'] as string | undefined) ??
    (attrs.label as string | undefined) ??
    '',
)
</script>

<template>
  <Button v-tooltip.top="{ value: tooltipText, disabled: !tooltipText }" v-bind="$attrs">
    <slot />
  </Button>
</template>
