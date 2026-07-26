<script setup lang="ts">
import { ref } from 'vue'
import Popover from 'primevue/popover'

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

const panel = ref<InstanceType<typeof Popover>>()

function toggle(event: MouseEvent): void {
  panel.value?.toggle(event)
}

function hide(): void {
  panel.value?.hide()
}

defineExpose({ hide })
</script>

<template>
  <span class="column-filter">
    <span>{{ label }}</span>
    <button
      type="button"
      class="column-filter__toggle"
      :class="{ 'column-filter__toggle--active': active }"
      :aria-label="toggleLabel"
      :title="toggleLabel"
      @click.stop="toggle"
    >
      <i :class="active ? 'pi pi-filter-fill' : 'pi pi-search'" aria-hidden="true" />
    </button>

    <Popover ref="panel" @show="emit('show')">
      <div
        class="column-filter__panel"
        :class="{ 'column-filter__panel--wide': wide }"
        @click.stop
        @keydown.stop
      >
        <slot />
        <button
          v-if="showClear"
          type="button"
          class="column-filter__clear"
          :aria-label="clearLabel"
          :title="$t('table.clear')"
          @click="emit('clear')"
        >
          <i class="pi pi-times" aria-hidden="true" />
        </button>
      </div>
    </Popover>
  </span>
</template>

<style scoped>
.column-filter {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.column-filter__toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--ca-text-muted);
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

.column-filter__toggle i {
  font-size: 13px;
}

.column-filter__panel {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 240px;
}

.column-filter__panel--wide {
  min-width: 280px;
}

.column-filter__clear {
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
  cursor: pointer;
}

.column-filter__clear:hover {
  color: var(--ca-danger-ink);
}
</style>
