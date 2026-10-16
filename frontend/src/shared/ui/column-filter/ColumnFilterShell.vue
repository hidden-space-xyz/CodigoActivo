<script setup lang="ts">
import { ref } from 'vue'

import { AppIcon } from '@/shared/ui/app-icon'

withDefaults(
  defineProps<{
    /** Column header text rendered next to the toggle, or the text of the toolbar pill. */
    label: string
    /** Highlights the toggle and swaps its icon for a filled filter icon. */
    active: boolean
    /** Accessible name of the toggle button, also its title in a header. */
    toggleLabel: string
    /** Accessible name of the clear button inside the panel. */
    clearLabel: string
    /** Shows the clear button next to the slotted control. */
    showClear: boolean
    /** Raises the panel's minimum width for wider controls such as date ranges. */
    wide?: boolean
    /** Icon of the inactive toggle, hinting at the kind of filter. */
    icon?: 'search' | 'filter' | 'calendar'
    /** `header` sits next to a column title; `toolbar` is a labelled pill above the table. */
    variant?: 'header' | 'toolbar'
  }>(),
  { wide: false, icon: 'search', variant: 'header' },
)

const emit = defineEmits<{
  /** Fired after the popover finishes opening, e.g. to focus the slotted input. */
  show: []
  /** Fired when the clear button is pressed; the parent resets its value and hides the panel. */
  clear: []
}>()

const visible = ref(false)
const panelEl = ref<HTMLElement>()

function hide(): void {
  visible.value = false
}

defineExpose({
  /** Closes the popover; wrappers call it after applying or clearing a filter. */
  hide,
})
</script>

<template>
  <span class="column-filter">
    <span v-if="variant === 'header'" class="column-filter__label">{{ label }}</span>
    <el-popover
      v-model:visible="visible"
      trigger="click"
      :placement="variant === 'toolbar' ? 'bottom-start' : 'bottom'"
      width="auto"
      @after-enter="emit('show')"
    >
      <template #reference>
        <button
          v-if="variant === 'toolbar'"
          type="button"
          class="column-filter__pill"
          :class="{ 'column-filter__pill--active': active }"
          :aria-label="toggleLabel"
          @click.stop
        >
          <AppIcon :name="active ? 'filter-fill' : icon" class="column-filter__pill-icon" />
          {{ label }}
        </button>
        <button
          v-else
          type="button"
          class="column-filter__toggle"
          :class="{ 'column-filter__toggle--active': active }"
          :aria-label="toggleLabel"
          :title="toggleLabel"
          @click.stop
        >
          <AppIcon :name="active ? 'filter-fill' : icon" />
        </button>
      </template>

      <div
        ref="panelEl"
        class="column-filter__panel"
        :class="{ 'column-filter__panel--wide': wide }"
        @click.stop
        @keydown.stop
      >
        <slot :panel="panelEl ?? ''" />
        <button
          v-if="showClear"
          type="button"
          class="column-filter__clear"
          :aria-label="clearLabel"
          :title="$t('table.clear')"
          @click="emit('clear')"
        >
          <AppIcon name="times" />
        </button>
      </div>
    </el-popover>
  </span>
</template>

<style scoped>
.column-filter {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  min-width: 0;
  max-width: 100%;
  white-space: nowrap;
}

.column-filter__label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}

.column-filter__toggle {
  position: relative;
  flex: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--ca-text-muted);
  font-size: 13px;
  cursor: pointer;
  transition:
    color 0.15s ease,
    background 0.15s ease;
}

.column-filter__toggle:hover {
  color: var(--ca-text);
  background: color-mix(in srgb, var(--ca-text) 12%, transparent);
}

.column-filter__toggle--active,
.column-filter__toggle--active:hover {
  color: var(--ca-orange-ink);
  background: var(--ca-orange-soft);
}

.column-filter__pill {
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 12px;
  border: 1px dashed var(--ca-border-strong-2);
  border-radius: 999px;
  background: transparent;
  color: var(--ca-text-muted);
  font: 600 13px/1 var(--ca-font-display);
  white-space: nowrap;
  cursor: pointer;
  transition:
    color 0.15s ease,
    background 0.15s ease,
    border-color 0.15s ease;
}

.column-filter__pill:hover {
  color: var(--ca-text);
  border-color: var(--ca-text-faint);
}

.column-filter__pill--active,
.column-filter__pill--active:hover {
  border: 1px solid transparent;
  background: var(--ca-orange-soft);
  color: var(--ca-orange-ink);
}

.column-filter__pill-icon {
  font-size: 14px;
}

.column-filter__toggle:focus-visible,
.column-filter__pill:focus-visible {
  outline: 2px solid var(--ca-orange-ink);
  outline-offset: 2px;
}

.column-filter__panel {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: min(240px, calc(100vw - 56px));
  max-width: calc(100vw - 32px);
}

.column-filter__panel--wide {
  min-width: min(280px, calc(100vw - 56px));
}

.column-filter__clear {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  flex: 0 0 auto;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--ca-text-muted);
  font-size: 15px;
  cursor: pointer;
}

.column-filter__clear:hover {
  color: var(--ca-danger-ink);
}

@media (pointer: coarse) {
  .column-filter__toggle::after,
  .column-filter__pill::after,
  .column-filter__clear::after {
    content: '';
    position: absolute;
    inset: -9px -3px;
  }

  .column-filter__panel {
    gap: 10px;
  }
}
</style>
