<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { nationalIdError, normalizeNationalId } from '@/shared/lib'

const props = defineProps<{
  /** DNI or NIE as typed; it is normalized when the input loses focus. */
  modelValue: string
  /** Id of the native input, so the form's own `<label for>` can point at it. */
  id: string
  /**
   * Reveals the problem even before the input is left or while it is blank, typically once the
   * form was submitted. Otherwise it only appears after leaving the input with something typed.
   */
  showErrors: boolean
}>()

const emit = defineEmits<{
  /** Fired on every keystroke, and on blur with the uppercase value without spaces or hyphens. */
  'update:modelValue': [value: string]
}>()

const draft = ref(props.modelValue)
const touched = ref(false)

watch(
  () => props.modelValue,
  (value) => {
    draft.value = value
  },
)

const problem = computed(() => nationalIdError(draft.value))
const visibleProblem = computed(() =>
  props.showErrors || (touched.value && draft.value.trim() !== '') ? problem.value : null,
)

function update(value: string): void {
  draft.value = value
  emit('update:modelValue', value)
}

function onBlur(): void {
  touched.value = true
  const normalized = normalizeNationalId(draft.value)
  if (normalized !== draft.value) update(normalized)
}
</script>

<template>
  <div class="national-id-input">
    <el-input
      :id="id"
      :model-value="draft"
      autocomplete="off"
      autocapitalize="characters"
      spellcheck="false"
      :maxlength="12"
      :class="{ 'ca-invalid': visibleProblem !== null }"
      required
      @update:model-value="update"
      @blur="onBlur"
    />
    <small v-if="visibleProblem === 'letter'" class="national-id-input__error">{{
      $t('validation.nationalIdLetter')
    }}</small>
    <small v-else-if="visibleProblem === 'format'" class="national-id-input__error">{{
      $t('validation.nationalIdFormat')
    }}</small>
  </div>
</template>

<style scoped>
.national-id-input {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.national-id-input__error {
  font-size: 12.5px;
  color: var(--ca-danger-ink);
}

.ca-invalid {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}
</style>
