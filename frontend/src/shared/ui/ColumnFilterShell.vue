<script setup lang="ts">
import { ref } from 'vue'

import AppIcon from './AppIcon.vue'

defineProps<{
  label: string
  active: boolean
  toggleLabel: string
  clearLabel: string
  showClear: boolean
  wide?: boolean
}>()

const emit = defineEmits<{
  show: []
  clear: []
}>()

const visible = ref(false)
const panelEl = ref<HTMLElement>()

function hide(): void {
  visible.value = false
}

defineExpose({ hide })
</script>

<template>
  <span class="column-filter">
    <span>{{ label }}</span>
    <el-popover
      v-model:visible="visible"
      trigger="click"
      placement="bottom"
      width="auto"
      @after-enter="emit('show')"
    >
      <template #reference>
        <button
          type="button"
          class="column-filter__toggle"
          :class="{ 'column-filter__toggle--active': active }"
          :aria-label="toggleLabel"
          :title="toggleLabel"
          @click.stop
        >
          <AppIcon :name="active ? 'filter-fill' : 'search'" />
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
  gap: 6px;
}

.column-filter__toggle {
  position: relative;
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

.column-filter__toggle--active {
  color: var(--ca-orange);
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
