import { computed, reactive, ref } from 'vue'

import { i18n, type TranslationKey } from '@/shared/i18n'

/** Why a field was refused: the message to show under it, or `true` when its red border is enough. */
export type FormProblem = TranslationKey | true

/** What reading a draft yields: the problems by field and, only without any, the value to send. */
export interface FormReading<TField extends string, TValue> {
  readonly problems: Partial<Record<TField, FormProblem>>
  readonly value: TValue | null
}

/** Where a form starts and how it reads what the user typed. */
interface FormOptions<TDraft extends object, TField extends string, TValue> {
  /** Blank draft the inputs start from. */
  readonly initial: () => TDraft
  /** Validates and normalizes a draft; pure, so its rules are tested without a component. */
  readonly read: (draft: TDraft) => FormReading<TField, TValue>
}

/**
 * Form state around a pure `read` function ("parse, don't validate"): the draft the inputs bind
 * to and, once the form was submitted, which fields are refused (`invalid`) and the translated
 * message of those that have one (`errors`). `submit` returns the value to send, or `null` while
 * something is wrong.
 */
export function useForm<TDraft extends object, TField extends string, TValue>(
  options: FormOptions<TDraft, TField, TValue>,
) {
  const draft = reactive(options.initial()) as TDraft
  const submitted = ref(false)
  const reading = computed(() => options.read(draft))

  const errors = computed(() => {
    const messages: Partial<Record<TField, string>> = {}
    if (!submitted.value) return messages
    const problems = Object.entries(reading.value.problems) as [TField, FormProblem][]
    for (const [field, problem] of problems)
      if (problem !== true) messages[field] = i18n.global.t(problem)
    return messages
  })

  function invalid(field: TField): boolean {
    return submitted.value && reading.value.problems[field] !== undefined
  }

  function reset(values: Partial<TDraft> = {}): void {
    Object.assign(draft, options.initial(), values)
    submitted.value = false
  }

  function submit(): TValue | null {
    submitted.value = true
    return reading.value.value
  }

  return { draft, submitted, errors, invalid, reset, submit }
}
