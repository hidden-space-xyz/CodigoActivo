<script setup lang="ts">
import { computed } from 'vue'

/**
 * Attributes handed to the control: the id its label points to, the refused look and, for controls
 * that forward them to their input, the ARIA state that links the message.
 */
interface FieldControl {
  readonly id: string | undefined
  readonly class: { readonly 'ca-invalid': boolean }
  readonly 'aria-invalid': boolean
  readonly 'aria-describedby': string | undefined
}

const props = defineProps<{
  /** Id of the control the label names; its message takes the same id followed by `-error`. */
  for?: string | undefined
  /** Visible name of the field. */
  label: string
  /** Shows the control as refused even when there is no message to explain it. */
  invalid?: boolean | undefined
  /** Message shown under the control while its value is refused. */
  error?: string | null | undefined
  /** Test id of the message, for the end-to-end suite. */
  errorTestid?: string | undefined
  /** Note shown under the control while there is no message. */
  hint?: string | undefined
  /** Spans both columns of a `ca-form-grid`. */
  wide?: boolean
}>()

defineSlots<{
  /** The control, given the attributes that tie it to the label and to the message. */
  default(slot: { control: FieldControl; invalid: boolean }): unknown
}>()

const refused = computed(() => props.invalid || !!props.error)
const errorId = computed(() => (props.for ? `${props.for}-error` : undefined))
const control = computed<FieldControl>(() => ({
  id: props.for,
  class: { 'ca-invalid': refused.value },
  'aria-invalid': refused.value,
  'aria-describedby': props.error ? errorId.value : undefined,
}))
</script>

<template>
  <div class="ca-field" :class="{ 'ca-field--wide': wide }">
    <label v-if="props.for" class="ca-field__label" :for="props.for">{{ label }}</label>
    <div v-else class="ca-field__label">{{ label }}</div>
    <slot :control="control" :invalid="refused" />
    <small v-if="error" :id="errorId" class="ca-field__error" :data-testid="errorTestid">{{
      error
    }}</small>
    <small v-else-if="hint" class="ca-field__hint">{{ hint }}</small>
  </div>
</template>
