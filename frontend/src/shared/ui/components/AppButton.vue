<script setup lang="ts">
import { computed, useAttrs } from 'vue'
import Button from 'primevue/button'

// Drop-in replacement for PrimeVue's Button that always shows a tooltip, so icon-only
// buttons (e.g. the action buttons in admin tables) explain themselves on hover. The
// tooltip text defaults to the button's aria-label, then its label; pass `tooltip` to override.
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
