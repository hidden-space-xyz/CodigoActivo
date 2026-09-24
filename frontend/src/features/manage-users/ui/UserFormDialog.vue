<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'

import { genderOptions } from '@/entities/user'
import type { UpdateUserInput, User } from '@/entities/user'
import type { Gender } from '@/shared/api/generated/models'
import {
  ageFrom,
  isValidNationalId,
  normalizeNationalId,
  parseDateOnly,
  toDateOnly,
} from '@/shared/lib'

const DATE_FORMAT = 'DD/MM/YYYY'

const props = defineProps<{
  /** Opens the dialog; each time it opens the form is repopulated from `user`. */
  visible: boolean
  /** User being edited; the dialog only edits existing users. */
  user: User | null
  /** Shows a loading state on the save button while the parent persists the changes. */
  saving: boolean
  /**
   * Rejection reported by the parent, such as an incorrect password; empty hides it. A non-empty
   * value also reveals the password field, so the server can ask for it when this form did not.
   */
  error: string
}>()

const emit = defineEmits<{
  /** Fired with `false` when the dialog is closed or dismissed. */
  'update:visible': [value: boolean]
  /**
   * Fired with the validated changes. A dependent always reports `null` email, phones and DNI/NIE
   * and no promotional consent, because the server keeps the guardian's details, plus a birth date
   * that keeps it a minor whenever it changes, so an unchanged birth date of a dependent that has
   * come of age is still accepted; a standalone account reports a `null` birth date, its normalized
   * DNI/NIE and its consent, and blank contact values are sent as `null`. The user's current
   * `parentId` is preserved. `currentPassword` carries the signed-in user's password when the
   * change replaces the email, the phone or the secondary phone of the account or the server already
   * refused one, and is `null` otherwise.
   */
  submit: [body: UpdateUserInput]
}>()

interface UserForm {
  firstName: string
  lastName: string
  email: string
  phone: string
  secondaryPhone: string
  birthDate: Date | null
  nationalId: string
  confirmNationalId: string
  promotionalConsent: boolean
  gender: Gender | null
  currentPassword: string
}

const form = reactive<UserForm>({
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  secondaryPhone: '',
  birthDate: null,
  nationalId: '',
  confirmNationalId: '',
  promotionalConsent: false,
  gender: null,
  currentPassword: '',
})
const submitted = ref(false)
const genders = genderOptions()

function disabledBirthDate(date: Date): boolean {
  return date > new Date()
}

const isDependent = computed(() => Boolean(props.user?.parentId))
const isMinorBirthDate = computed(() => {
  const age = ageFrom(form.birthDate)
  return age !== null && age < 18
})
const birthDateInvalid = computed(
  () => isDependent.value && (!form.birthDate || form.birthDate > new Date()),
)
const birthDateChanged = computed(
  () => form.birthDate !== null && toDateOnly(form.birthDate) !== props.user?.birthDate,
)
const childNotMinor = computed(
  () =>
    isDependent.value &&
    !birthDateInvalid.value &&
    birthDateChanged.value &&
    !isMinorBirthDate.value,
)
const nationalIdInvalid = computed(() => !isDependent.value && !isValidNationalId(form.nationalId))
const nationalIdsMismatch = computed(
  () =>
    !isDependent.value &&
    normalizeNationalId(form.confirmNationalId) !== normalizeNationalId(form.nationalId),
)
const emailInvalid = computed(() => {
  if (isDependent.value) return false
  const value = form.email.trim()
  return value.length > 0 && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
})
const contactMissing = computed(
  () => !isDependent.value && (!form.email.trim() || !form.phone.trim()),
)
const storedEmail = computed(() => props.user?.email ?? '')
const storedPhone = computed(() => props.user?.phone ?? '')
const storedSecondaryPhone = computed(() => props.user?.secondaryPhone ?? '')
const secondaryPhoneRepeated = computed(
  () =>
    !isDependent.value &&
    !!form.secondaryPhone.trim() &&
    form.secondaryPhone.trim() === form.phone.trim(),
)
const replacesIdentifiers = computed(() => {
  if (isDependent.value) return false
  return (
    form.email.trim().toLowerCase() !== storedEmail.value.toLowerCase() ||
    form.phone.trim() !== storedPhone.value ||
    form.secondaryPhone.trim() !== storedSecondaryPhone.value
  )
})
const passwordRejected = ref(false)
const requiresPassword = computed(() => replacesIdentifiers.value || passwordRejected.value)
const passwordMissing = computed(() => requiresPassword.value && !form.currentPassword)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    passwordRejected.value = false
    form.firstName = props.user?.firstName ?? ''
    form.lastName = props.user?.lastName ?? ''
    form.email = props.user?.email ?? ''
    form.phone = props.user?.phone ?? ''
    form.secondaryPhone = props.user?.secondaryPhone ?? ''
    form.birthDate = parseDateOnly(props.user?.birthDate)
    form.nationalId = props.user?.nationalId ?? ''
    form.confirmNationalId = form.nationalId
    form.promotionalConsent = props.user?.promotionalConsent ?? false
    form.gender = props.user?.gender ?? null
    form.currentPassword = ''
  },
)

watch(
  () => props.error,
  (value) => {
    if (value) passwordRejected.value = true
  },
)

function close(): void {
  emit('update:visible', false)
}

function save(): void {
  submitted.value = true
  if (
    !form.firstName.trim() ||
    !form.lastName.trim() ||
    birthDateInvalid.value ||
    childNotMinor.value ||
    nationalIdInvalid.value ||
    nationalIdsMismatch.value ||
    emailInvalid.value ||
    contactMissing.value ||
    secondaryPhoneRepeated.value ||
    passwordMissing.value ||
    !form.gender
  ) {
    return
  }
  const gender = form.gender
  const dependent = isDependent.value
  const body: UpdateUserInput = {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: dependent || !form.email.trim() ? null : form.email.trim(),
    phone: dependent || !form.phone.trim() ? null : form.phone.trim(),
    secondaryPhone: dependent || !form.secondaryPhone.trim() ? null : form.secondaryPhone.trim(),
    birthDate: dependent && form.birthDate ? toDateOnly(form.birthDate) : null,
    nationalId: dependent ? null : normalizeNationalId(form.nationalId),
    promotionalConsent: !dependent && form.promotionalConsent,
    gender,
    parentId: props.user?.parentId ?? null,
    currentPassword: requiresPassword.value ? form.currentPassword : null,
  }
  emit('submit', body)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="$t('features.manageUsers.editHeader')"
    width="min(480px, 92vw)"
    @update:model-value="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__row">
        <div class="form__field">
          <label for="user-first-name">{{ $t('common.firstName') }}</label>
          <el-input
            id="user-first-name"
            v-model="form.firstName"
            :maxlength="120"
            :class="{ 'ca-invalid': submitted && !form.firstName.trim() }"
          />
        </div>
        <div class="form__field">
          <label for="user-last-name">{{ $t('common.lastName') }}</label>
          <el-input
            id="user-last-name"
            v-model="form.lastName"
            :maxlength="120"
            :class="{ 'ca-invalid': submitted && !form.lastName.trim() }"
          />
        </div>
      </div>
      <div v-if="isDependent" class="form__field">
        <label for="user-birth-date">{{ $t('common.birthDate') }}</label>
        <el-date-picker
          id="user-birth-date"
          v-model="form.birthDate"
          type="date"
          :format="DATE_FORMAT"
          :disabled-date="disabledBirthDate"
          :class="{ 'ca-invalid': submitted && (birthDateInvalid || childNotMinor) }"
        />
        <small v-if="submitted && birthDateInvalid" class="form__error">{{
          $t('features.manageUsers.birthDateInvalid')
        }}</small>
        <small v-else-if="submitted && childNotMinor" class="form__error">{{
          $t('features.manageUsers.childBirthDateNotMinor')
        }}</small>
      </div>
      <div v-else class="form__row">
        <div class="form__field">
          <label for="user-national-id">{{ $t('common.nationalId') }}</label>
          <el-input
            id="user-national-id"
            v-model="form.nationalId"
            autocomplete="off"
            autocapitalize="characters"
            spellcheck="false"
            :maxlength="12"
            :class="{ 'ca-invalid': submitted && nationalIdInvalid }"
          />
          <small v-if="submitted && nationalIdInvalid" class="form__error">{{
            $t('validation.nationalIdInvalid')
          }}</small>
        </div>
        <div class="form__field">
          <label for="user-national-id-confirm">{{ $t('common.confirmNationalId') }}</label>
          <el-input
            id="user-national-id-confirm"
            v-model="form.confirmNationalId"
            autocomplete="off"
            autocapitalize="characters"
            spellcheck="false"
            :maxlength="12"
            :class="{ 'ca-invalid': submitted && nationalIdsMismatch }"
          />
          <small v-if="submitted && nationalIdsMismatch" class="form__error">{{
            $t('validation.nationalIdsMismatch')
          }}</small>
        </div>
      </div>
      <div class="form__field">
        <label for="user-gender">{{ $t('common.gender') }}</label>
        <el-select
          id="user-gender"
          v-model="form.gender"
          :class="{ 'ca-invalid': submitted && !form.gender }"
        >
          <el-option
            v-for="option in genders"
            :key="option.value"
            :label="option.label"
            :value="option.value"
          />
        </el-select>
        <small v-if="submitted && !form.gender" class="form__error">{{
          $t('validation.genderRequired')
        }}</small>
      </div>
      <p v-if="isDependent" class="form__hint">
        {{ $t('features.manageUsers.dependentContact') }}
      </p>
      <template v-else>
        <div class="form__field">
          <label for="user-email">{{ $t('common.email') }}</label>
          <el-input
            id="user-email"
            v-model="form.email"
            type="email"
            :maxlength="256"
            :class="{
              'ca-invalid': submitted && (emailInvalid || (contactMissing && !form.email.trim())),
            }"
          />
          <small v-if="submitted && emailInvalid" class="form__error">{{
            $t('validation.emailFormat')
          }}</small>
        </div>
        <div class="form__row">
          <div class="form__field">
            <label for="user-phone">{{ $t('common.phone') }}</label>
            <el-input
              id="user-phone"
              v-model="form.phone"
              type="tel"
              :maxlength="40"
              :class="{
                'ca-invalid': submitted && contactMissing && !form.phone.trim(),
              }"
            />
            <small v-if="submitted && contactMissing" class="form__error">{{
              $t('features.manageUsers.contactRequired')
            }}</small>
          </div>
          <div class="form__field">
            <label for="user-secondary-phone">{{ $t('common.secondaryPhoneOptional') }}</label>
            <el-input
              id="user-secondary-phone"
              v-model="form.secondaryPhone"
              type="tel"
              :maxlength="40"
              :class="{ 'ca-invalid': submitted && secondaryPhoneRepeated }"
            />
            <small v-if="submitted && secondaryPhoneRepeated" class="form__error">{{
              $t('validation.secondaryPhoneSameAsPrimary')
            }}</small>
          </div>
        </div>
        <div class="form__consent">
          <el-checkbox id="user-promotional-consent" v-model="form.promotionalConsent" />
          <label for="user-promotional-consent">{{ $t('common.promotionalConsentOption') }}</label>
        </div>
      </template>
      <div v-if="requiresPassword" class="form__field">
        <label for="user-current-password">{{
          $t('features.manageUsers.identifierChange.passwordLabel')
        }}</label>
        <p class="form__hint">{{ $t('features.manageUsers.identifierChange.message') }}</p>
        <el-input
          id="user-current-password"
          v-model="form.currentPassword"
          type="password"
          show-password
          autocomplete="current-password"
          :maxlength="128"
          :class="{ 'ca-invalid': (submitted && passwordMissing) || !!error }"
        />
        <small v-if="error" class="form__error">{{ error }}</small>
        <small v-else-if="submitted && passwordMissing" class="form__error">{{
          $t('features.manageUsers.identifierChange.passwordRequired')
        }}</small>
      </div>
    </form>

    <template #footer>
      <Button :label="$t('common.cancel')" text :disabled="saving" @click="close" />
      <Button :label="$t('common.save')" type="primary" :loading="saving" @click="save" />
    </template>
  </el-dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-top: 6px;
}

.form__row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
}

.form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.form__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}

.form__consent {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}

.form__consent label {
  font-size: 13px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  cursor: pointer;
}

.form__hint {
  margin: 0;
  font-size: 12.5px;
  line-height: 1.4;
  color: var(--ca-text-muted);
}

.form :deep(.el-select),
.form :deep(.el-date-editor) {
  width: 100%;
}

.form :deep(.ca-invalid) {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}

.form :deep(.ca-invalid .el-select__wrapper) {
  box-shadow: 0 0 0 1px var(--ca-danger) inset;
}

@media (max-width: 640px) {
  .form__row {
    grid-template-columns: 1fr;
  }
}
</style>
