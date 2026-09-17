<script setup lang="ts">
withDefaults(
  defineProps<{
    /** Kicker text, rendered uppercase in the monospace font. */
    text: string
    /** Any CSS color value, preferably a `--ca-*` variable. */
    color?: string
  }>(),
  {
    color: 'var(--ca-orange-ink)',
  },
)
</script>

<template>
  <div class="eyebrow" :style="{ color }">{{ text }}</div>
</template>

<style scoped>
.eyebrow {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  margin-bottom: 10px;
}
</style>
