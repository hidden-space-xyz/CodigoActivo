<script setup lang="ts">
defineProps<{
  /** Kicker text, rendered uppercase in the monospace font. */
  text: string
}>()
</script>

<template>
  <div class="eyebrow">{{ text }}</div>
</template>

<style scoped>
.eyebrow {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  margin-bottom: 10px;
  color: var(--ca-orange-ink);
}
</style>
