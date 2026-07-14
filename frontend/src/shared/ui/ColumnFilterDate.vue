<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import DatePicker from 'primevue/datepicker'
import Popover from 'primevue/popover'

const props = defineProps<{
  modelValue: (Date | null)[] | null
  label: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: (Date | null)[] | null]
  apply: []
}>()

const panel = ref<InstanceType<typeof Popover>>()
const draft = ref<(Date | null)[] | null>(props.modelValue)

const active = computed(() => (props.modelValue ?? []).some((date) => date instanceof Date))

watch(
  () => props.modelValue,
  (value) => {
    draft.value = value ?? null
  },
)

function commit(): void {
  const range = draft.value ?? []
  emit('update:modelValue', range.some((date) => date instanceof Date) ? draft.value : null)
  emit('apply')
}

function onSelect(): void {
  commit()
  if (draft.value?.[0] instanceof Date && draft.value[1] instanceof Date) panel.value?.hide()
}

function clear(): void {
  draft.value = null
  commit()
  panel.value?.hide()
}

function toggle(event: MouseEvent): void {
  panel.value?.toggle(event)
}
</script>

<template>
  <span class="column-filter">
    <span>{{ label }}</span>
    <button
      type="button"
      class="column-filter__toggle"
      :class="{ 'column-filter__toggle--active': active }"
      :aria-label="`Filtrar por ${label}`"
      :title="`Filtrar por ${label}`"
      @click.stop="toggle"
    >
      <i :class="active ? 'pi pi-filter-fill' : 'pi pi-search'" aria-hidden="true" />
    </button>

    <Popover ref="panel">
      <div class="column-filter__panel" @click.stop @keydown.stop>
        <DatePicker
          v-model="draft"
          selection-mode="range"
          :manual-input="false"
          date-format="dd/mm/yy"
          placeholder="Desde – Hasta"
          show-icon
          fluid
          @update:model-value="onSelect"
        />
        <button
          v-if="active || draft"
          type="button"
          class="column-filter__clear"
          aria-label="Limpiar filtro"
          title="Limpiar"
          @click="clear"
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
