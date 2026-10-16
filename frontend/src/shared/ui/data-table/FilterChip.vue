<script setup lang="ts">
import { AppIcon } from '@/shared/ui/app-icon'

defineProps<{
  /** Chip text, such as "Estado: Activo"; truncated with its full text as a tooltip. */
  label: string
  /** Accessible name of the remove button. */
  removeLabel: string
}>()

const emit = defineEmits<{
  /** Fired when the remove button is pressed; the owner clears the filter. */
  remove: []
}>()
</script>

<template>
  <li class="filter-chip">
    <span class="filter-chip__label" :title="label">{{ label }}</span>
    <button
      type="button"
      class="filter-chip__remove"
      :aria-label="removeLabel"
      @click="emit('remove')"
    >
      <AppIcon name="times" />
    </button>
  </li>
</template>

<style scoped>
.filter-chip {
  display: inline-flex;
  flex: none;
  align-items: center;
  gap: 2px;
  max-width: 100%;
  height: 28px;
  padding: 0 3px 0 10px;
  border-radius: 999px;
  background: var(--ca-orange-soft);
  color: var(--ca-orange-ink);
  font: 600 12.5px/1 var(--ca-font-display);
  white-space: nowrap;
}

.filter-chip__label {
  min-width: 0;
  max-width: 280px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.filter-chip__remove {
  position: relative;
  flex: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  padding: 0;
  border: none;
  border-radius: 50%;
  background: transparent;
  color: inherit;
  font-size: 12px;
  cursor: pointer;
  transition: background-color 0.15s ease;
}

.filter-chip__remove:hover {
  background: color-mix(in srgb, var(--ca-orange) 25%, transparent);
}

.filter-chip__remove:focus-visible {
  outline: 2px solid var(--ca-orange-ink);
  outline-offset: 1px;
}

@media (pointer: coarse) {
  .filter-chip__remove::after {
    content: '';
    position: absolute;
    inset: -8px -6px;
  }
}
</style>
