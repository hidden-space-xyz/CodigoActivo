import { computed, ref, toValue, type MaybeRefOrGetter } from 'vue'

import { toDateOnly } from '@/shared/lib/date'
import { useForm, type FormProblem, type FormReading } from '@/shared/lib/form'

import {
  isSameEmail,
  parseDependentPerson,
  parseIndependentPerson,
  personProblemKey,
  type DependentPerson,
  type IndependentPerson,
  type ParsedPerson,
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

/** A person the form accepted, tagged with the rules that read it. */
type ReadPerson =
  | { readonly kind: 'independent'; readonly person: IndependentPerson }
  | { readonly kind: 'dependent'; readonly person: DependentPerson }

function toReading<T>(
  parsed: ParsedPerson<T>,
  tag: (person: T) => ReadPerson,
): FormReading<PersonField, ReadPerson> {
  const problems: Partial<Record<PersonField, FormProblem>> = {}
  for (const field of Object.keys(parsed.problems) as PersonField[]) {
    const problem = parsed.problems[field]
    if (problem) problems[field] = personProblemKey(field, problem)
  }
  return { problems, value: parsed.person ? tag(parsed.person) : null }
}

/**
 * Form state for creating or editing a person under the rules of its kind, built on `useForm`. The
 * draft is what the inputs bind to; `errors` holds a translated message per refused field once the
 * form was submitted. Editing an independent account asks for `currentPassword` whenever its
 * email, phone or secondary phone changes (the email ignoring case), or after `rejectPassword`
 * reports that the server refused one. `submit` returns the normalized person, or `null` while
 * something is wrong.
 */
export function usePersonForm(options: PersonFormOptions) {
  const today = options.today ?? toDateOnly(new Date())
  const kind = computed(() => toValue(options.kind))
  const stored = computed(() => toValue(options.stored) ?? null)
  const currentPassword = ref('')
  const passwordRejected = ref(false)

  const form = useForm({
    initial: emptyDraft,
    read: (draft) =>
      kind.value === 'dependent'
        ? toReading(
            parseDependentPerson(draft, {
              today,
              storedBirthDate: stored.value?.birthDate ?? null,
            }),
            (person) => ({ kind: 'dependent', person }),
          )
        : toReading(parseIndependentPerson(draft), (person) => ({ kind: 'independent', person })),
  })
  const { draft } = form

  const replacesContact = computed(() => {
    const previous = stored.value
    return (
      previous !== null &&
      (!isSameEmail(draft.email, previous.email) ||
        draft.phone.trim() !== (previous.phone ?? '') ||
        draft.secondaryPhone.trim() !== (previous.secondaryPhone ?? ''))
    )
  })
  const requiresPassword = computed(
    () => kind.value === 'independent' && (replacesContact.value || passwordRejected.value),
  )
  const passwordMissing = computed(() => requiresPassword.value && !currentPassword.value)

  function load(values?: Partial<Readonly<PersonDraft>> | null): void {
    form.reset(values ? pickDraft(values) : {})
    currentPassword.value = ''
    passwordRejected.value = false
  }

  function rejectPassword(): void {
    passwordRejected.value = true
  }

  function submit(): PersonSubmission | null {
    const read = form.submit()
    if (passwordMissing.value || !read) return null
    if (read.kind === 'dependent') {
      return { kind: 'dependent', person: read.person, currentPassword: null }
    }
    return {
      kind: 'independent',
      person: read.person,
      currentPassword: requiresPassword.value ? currentPassword.value : null,
    }
  }

  return {
    draft,
    currentPassword,
    submitted: form.submitted,
    errors: form.errors,
    requiresPassword,
    passwordMissing,
    load,
    rejectPassword,
    submit,
  }
}
