import { computed, reactive, ref, toValue, type MaybeRefOrGetter } from 'vue'

import { toDateOnly } from '@/shared/lib'

import {
  parseDependentPerson,
  parseIndependentPerson,
  personProblemMessage,
  type PersonDraft,
  type PersonField,
  type PersonSubmission,
} from './person'

/** Rules a person form applies: those of an independent account or those of a dependent. */
type PersonKind = 'independent' | 'dependent'

/** Stored values a form compares the draft with to tell what changed; missing ones count as empty. */
type StoredPerson = Partial<Pick<PersonDraft, 'email' | 'phone' | 'secondaryPhone' | 'birthDate'>>

/** Where a person form gets its rules and the record it edits. */
export interface PersonFormOptions {
  /** Which rules apply; a getter lets one dialog edit both kinds of account. */
  readonly kind: MaybeRefOrGetter<PersonKind>
  /**
   * Record being edited, or `null` while creating one. Replacing its email or phones requires the
   * password of whoever makes the change, and a dependent may keep its stored birth date.
   */
  readonly stored?: MaybeRefOrGetter<StoredPerson | null | undefined>
  /** Local day (`YYYY-MM-DD`) a dependent must be a minor on; defaults to today. */
  readonly today?: string
}

const DRAFT_FIELDS = [
  'firstName',
  'lastName',
  'gender',
  'email',
  'phone',
  'secondaryPhone',
  'nationalId',
  'promotionalConsent',
  'birthDate',
] as const satisfies readonly (keyof PersonDraft)[]

function emptyDraft(): PersonDraft {
  return {
    firstName: '',
    lastName: '',
    gender: null,
    email: '',
    phone: '',
    secondaryPhone: '',
    nationalId: '',
    promotionalConsent: false,
    birthDate: '',
  }
}

function pickDraft(values: Partial<Readonly<PersonDraft>>): Partial<PersonDraft> {
  return Object.fromEntries(
    DRAFT_FIELDS.filter((field) => values[field] != null).map((field) => [field, values[field]]),
  )
}

/**
 * Form state for creating or editing a person under the rules of its kind. The draft is what the
 * inputs bind to; `errors` holds a translated message per refused field once the form was
 * submitted. Editing an independent account asks for `currentPassword` whenever its email, phone
 * or secondary phone changes (the email ignoring case), or after `rejectPassword` reports that the
 * server refused one. `submit` returns the normalized person, or `null` while something is wrong.
 */
export function usePersonForm(options: PersonFormOptions) {
  const draft = reactive<PersonDraft>(emptyDraft())
  const currentPassword = ref('')
  const submitted = ref(false)
  const passwordRejected = ref(false)
  const today = options.today ?? toDateOnly(new Date())

  const kind = computed(() => toValue(options.kind))
  const stored = computed(() => toValue(options.stored) ?? null)
  const parsed = computed(() =>
    kind.value === 'dependent'
      ? {
          kind: 'dependent' as const,
          ...parseDependentPerson(draft, {
            today,
            storedBirthDate: stored.value?.birthDate ?? null,
          }),
        }
      : { kind: 'independent' as const, ...parseIndependentPerson(draft) },
  )
  const replacesContact = computed(() => {
    const previous = stored.value
    return (
      previous !== null &&
      (draft.email.trim().toLowerCase() !== (previous.email ?? '').toLowerCase() ||
        draft.phone.trim() !== (previous.phone ?? '') ||
        draft.secondaryPhone.trim() !== (previous.secondaryPhone ?? ''))
    )
  })
  const requiresPassword = computed(
    () => kind.value === 'independent' && (replacesContact.value || passwordRejected.value),
  )
  const passwordMissing = computed(() => requiresPassword.value && !currentPassword.value)
  const errors = computed(() => {
    const messages: Partial<Record<PersonField, string>> = {}
    if (!submitted.value) return messages
    const { problems } = parsed.value
    for (const field of Object.keys(problems) as PersonField[]) {
      const problem = problems[field]
      if (problem) messages[field] = personProblemMessage(field, problem)
    }
    return messages
  })

  function load(values?: Partial<Readonly<PersonDraft>> | null): void {
    Object.assign(draft, emptyDraft(), values ? pickDraft(values) : {})
    currentPassword.value = ''
    submitted.value = false
    passwordRejected.value = false
  }

  function rejectPassword(): void {
    passwordRejected.value = true
  }

  function submit(): PersonSubmission | null {
    submitted.value = true
    const current = parsed.value
    if (passwordMissing.value || !current.person) return null
    if (current.kind === 'dependent') {
      return { kind: 'dependent', person: current.person, currentPassword: null }
    }
    return {
      kind: 'independent',
      person: current.person,
      currentPassword: requiresPassword.value ? currentPassword.value : null,
    }
  }

  return {
    draft,
    currentPassword,
    submitted,
    errors,
    requiresPassword,
    passwordMissing,
    load,
    rejectPassword,
    submit,
  }
}
