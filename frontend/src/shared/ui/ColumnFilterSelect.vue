<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Popover from 'primevue/popover'
import Select from 'primevue/select'

const props = defineProps<{
  modelValue: string | boolean | null
  label: string
  options: { label: string; value: string | boolean }[]
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string | boolean | null]
  apply: []
}>()

const panel = ref<InstanceType<typeof Popover>>()
const draft = ref<string | boolean | null>(props.modelValue)

const active = computed(() => props.modelValue !== null && props.modelValue !== undefined)

watch(
  () => props.modelValue,
  (value) => {
    draft.value = value ?? null
  },
)

function commit(): void {
  emit('update:modelValue', draft.value)
  emit('apply')
}

function onChange(): void {
  commit()
  panel.value?.hide()
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
      :aria-label="$t('table.filterBy', { label })"
      :title="$t('table.filterBy', { label })"
      @click.stop="toggle"
    >
      <i :class="active ? 'pi pi-filter-fill' : 'pi pi-search'" aria-hidden="true" />
    </button>

    <Popover ref="panel">
      <div class="column-filter__panel" @click.stop @keydown.stop>
        <Select
          v-model="draft"
          :options="options"
          option-label="label"
          option-value="value"
          :placeholder="$t('table.filterBy', { label })"
          show-clear
          fluid
          @change="onChange"
        />
        <button
          v-if="draft != null"
          type="button"
          class="column-filter__clear"
          :aria-label="$t('table.clearFilter')"
          :title="$t('table.clear')"
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
  min-width: 240px;
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
