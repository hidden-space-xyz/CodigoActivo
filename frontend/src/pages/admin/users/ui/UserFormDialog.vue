<script setup lang="ts">
import { computed, watch } from 'vue'

import {
  GENDERS,
  genderLabelKey,
  NationalIdInput,
  toUpdateUserInput,
  usePersonForm,
  type UpdateUserInput,
  type User,
} from '@/entities/user'
import { ActionButton } from '@/shared/ui/action-button'

const DATE_FORMAT = 'DD/MM/YYYY'
const VALUE_FORMAT = 'YYYY-MM-DD'

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
   * Fired with the changes once the person rules of `usePersonForm` accept them, shaped by
   * `toUpdateUserInput` and keeping the user's current `parentId`. `currentPassword` carries the
   * signed-in user's password when the change replaces the email, the phone or the secondary phone
   * of the account or the server already refused one, and is `null` otherwise.
   */
  submit: [body: UpdateUserInput]
}>()

function disabledBirthDate(date: Date): boolean {
  return date > new Date()
}

const isDependent = computed(() => Boolean(props.user?.parentId))
const {
  draft,
  currentPassword,
  submitted,
  errors,
  requiresPassword,
  passwordMissing,
  load,
  rejectPassword,
  submit,
} = usePersonForm({
  kind: () => (isDependent.value ? 'dependent' : 'independent'),
  stored: () => props.user,
})

watch(
  () => props.visible,
  (open) => {
    if (open) load(props.user)
  },
)

watch(
  () => props.error,
  (value) => {
    if (value) rejectPassword()
  },
)

function close(): void {
  emit('update:visible', false)
}

function save(): void {
  const submission = submit()
  if (submission) emit('submit', toUpdateUserInput(submission, props.user?.parentId ?? null))
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="$t('pages.admin.users.form.editHeader')"
    width="min(480px, 92vw)"
    @update:model-value="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__row">
        <div class="form__field">
          <label for="user-first-name">{{ $t('common.firstName') }}</label>
          <el-input
            id="user-first-name"
            v-model="draft.firstName"
            :maxlength="120"
            :class="{ 'ca-invalid': errors.firstName }"
          />
          <small v-if="errors.firstName" class="form__error">{{ errors.firstName }}</small>
        </div>
        <div class="form__field">
          <label for="user-last-name">{{ $t('common.lastName') }}</label>
          <el-input
            id="user-last-name"
            v-model="draft.lastName"
            :maxlength="120"
            :class="{ 'ca-invalid': errors.lastName }"
          />
          <small v-if="errors.lastName" class="form__error">{{ errors.lastName }}</small>
        </div>
      </div>
      <div class="form__row">
        <div v-if="isDependent" class="form__field">
          <label for="user-birth-date">{{ $t('common.birthDate') }}</label>
          <el-date-picker
            id="user-birth-date"
            v-model="draft.birthDate"
            type="date"
            :format="DATE_FORMAT"
            :value-format="VALUE_FORMAT"
            :disabled-date="disabledBirthDate"
            :class="{ 'ca-invalid': errors.birthDate }"
          />
          <small v-if="errors.birthDate" class="form__error">{{ errors.birthDate }}</small>
        </div>
        <div v-else class="form__field">
          <label for="user-national-id">{{ $t('common.nationalId') }}</label>
          <NationalIdInput
            id="user-national-id"
            v-model="draft.nationalId"
            :show-errors="submitted"
          />
        </div>
        <div class="form__field">
          <label for="user-gender">{{ $t('common.gender') }}</label>
          <el-select
            id="user-gender"
            v-model="draft.gender"
            :class="{ 'ca-invalid': errors.gender }"
          >
            <el-option
              v-for="gender in GENDERS"
              :key="gender"
              :label="$t(genderLabelKey(gender))"
              :value="gender"
            />
          </el-select>
          <small v-if="errors.gender" class="form__error">{{ errors.gender }}</small>
        </div>
      </div>
      <p v-if="isDependent" class="form__hint">
        {{ $t('pages.admin.users.form.dependentContact') }}
      </p>
      <template v-else>
        <div class="form__row">
          <div class="form__field">
            <label for="user-phone">{{ $t('common.phone') }}</label>
            <el-input
              id="user-phone"
              v-model="draft.phone"
              type="tel"
              :maxlength="40"
              :class="{ 'ca-invalid': errors.phone }"
            />
            <small v-if="errors.phone" class="form__error">{{ errors.phone }}</small>
          </div>
          <div class="form__field">
            <label for="user-secondary-phone">{{ $t('common.secondaryPhoneOptional') }}</label>
            <el-input
              id="user-secondary-phone"
              v-model="draft.secondaryPhone"
              type="tel"
              :maxlength="40"
              :class="{ 'ca-invalid': errors.secondaryPhone }"
            />
            <small v-if="errors.secondaryPhone" class="form__error">{{
              errors.secondaryPhone
            }}</small>
          </div>
        </div>
        <div class="form__field">
          <label for="user-email">{{ $t('common.email') }}</label>
          <el-input
            id="user-email"
            v-model="draft.email"
            type="email"
            :maxlength="256"
            :class="{ 'ca-invalid': errors.email }"
          />
          <small v-if="errors.email" class="form__error">{{ errors.email }}</small>
        </div>
        <div class="form__consent">
          <el-checkbox id="user-promotional-consent" v-model="draft.promotionalConsent" />
          <label for="user-promotional-consent">{{ $t('common.promotionalConsentOption') }}</label>
        </div>
      </template>
      <div v-if="requiresPassword" class="form__field">
        <label for="user-current-password">{{ $t('pages.admin.users.adminPassword.label') }}</label>
        <p class="form__hint">{{ $t('pages.admin.users.form.contactChange') }}</p>
        <el-input
          id="user-current-password"
          v-model="currentPassword"
          type="password"
          show-password
          autocomplete="current-password"
          :maxlength="128"
          :class="{ 'ca-invalid': (submitted && passwordMissing) || !!error }"
        />
        <small v-if="error" class="form__error">{{ error }}</small>
        <small v-else-if="submitted && passwordMissing" class="form__error">{{
          $t('pages.admin.users.adminPassword.form.problems.passwordRequired')
        }}</small>
      </div>
    </form>

    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="close" />
      <ActionButton :label="$t('common.save')" type="primary" :loading="saving" @click="save" />
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

@media (max-width: 640px) {
  .form__row {
    grid-template-columns: 1fr;
  }
}
</style>
