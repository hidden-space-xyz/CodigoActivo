<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import InputText from 'primevue/inputtext'
import Popover from 'primevue/popover'

const props = withDefaults(
  defineProps<{
    modelValue: string | number | null | undefined
    label: string
    placeholder?: string
    inputType?: 'text' | 'number'
    debounce?: number
  }>(),
  { placeholder: '', inputType: 'text', debounce: 300 },
)

const emit = defineEmits<{
  'update:modelValue': [value: string | number | null]
  apply: []
}>()

const panel = ref<InstanceType<typeof Popover>>()
const input = ref<InstanceType<typeof InputText>>()
const draft = ref(props.modelValue == null ? '' : String(props.modelValue))
let timer: ReturnType<typeof setTimeout> | undefined

const active = computed(
  () => props.modelValue !== null && props.modelValue !== undefined && props.modelValue !== '',
)

watch(
  () => props.modelValue,
  (value) => {
    const next = value == null ? '' : String(value)
    if (next !== draft.value.trim()) draft.value = next
  },
)

function commit(): void {
  const raw = draft.value.trim()
  const value = raw === '' ? null : props.inputType === 'number' ? Number(raw) : raw
  emit('update:modelValue', value)
  emit('apply')
}

function onInput(): void {
  if (timer) clearTimeout(timer)
  timer = setTimeout(commit, props.debounce)
}

function applyNow(): void {
  if (timer) clearTimeout(timer)
  commit()
  panel.value?.hide()
}

function cancel(): void {
  if (timer) clearTimeout(timer)
  draft.value = props.modelValue == null ? '' : String(props.modelValue)
  panel.value?.hide()
}

function clear(): void {
  draft.value = ''
  applyNow()
}

function toggle(event: MouseEvent): void {
  panel.value?.toggle(event)
}

async function focusInput(): Promise<void> {
  await nextTick()
  const el = (input.value as unknown as { $el?: HTMLElement } | undefined)?.$el
  el?.focus()
}

onBeforeUnmount(() => {
  if (timer) clearTimeout(timer)
})
</script>

<template>
  <span class="column-search">
    <span>{{ label }}</span>
    <button
      type="button"
      class="column-search__toggle"
      :class="{ 'column-search__toggle--active': active }"
      :aria-label="`Buscar por ${label}`"
      :title="`Buscar por ${label}`"
      @click.stop="toggle"
    >
      <i :class="active ? 'pi pi-filter-fill' : 'pi pi-search'" aria-hidden="true" />
    </button>

    <Popover ref="panel" @show="focusInput">
      <div class="column-search__panel" @click.stop @keydown.stop>
        <InputText
          ref="input"
          v-model="draft"
          :type="inputType"
          :placeholder="placeholder || `Buscar por ${label}`"
          fluid
          @input="onInput"
          @keydown.enter="applyNow"
          @keydown.esc="cancel"
        />
        <button
          v-if="draft"
          type="button"
          class="column-search__clear"
          aria-label="Limpiar búsqueda"
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
.column-search {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.column-search__toggle {
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

.column-search__toggle:hover {
  color: var(--ca-text);
  background: color-mix(in srgb, var(--ca-text) 12%, transparent);
}

.column-search__toggle--active {
  color: var(--ca-cyan);
}

.column-search__toggle i {
  font-size: 13px;
}

.column-search__panel {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 240px;
}

.column-search__clear {
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

.column-search__clear:hover {
  color: var(--ca-coral);
}
</style>
