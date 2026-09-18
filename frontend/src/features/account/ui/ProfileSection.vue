<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { useAccount } from '../model/useAccount'
import type { UpdateProfileInput } from '@/entities/account'
import { genderLabel, genderOptions } from '@/entities/user'
import { ApiError } from '@/shared/api'
import type { Gender } from '@/shared/api/generated/models'
import { BaseButton } from '@/shared/ui'
import { formatDate, getErrorMessage, toDateInput, todayIso, useCrudFeedback } from '@/shared/lib'

const { t } = useI18n()
const feedback = useCrudFeedback()
const { profile, updateProfile, changePassword } = useAccount()

const maxBirthDateIso = todayIso()
const user = computed(() => profile.data.value ?? null)

const genders = genderOptions()

const editVisible = ref(false)
const editSubmitted = ref(false)
const editError = ref('')
const editForm = reactive<{
  firstName: string
  lastName: string
  email: string
  phone: string
  birthDate: string
  gender: Gender | null
  currentPassword: string
}>({
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  birthDate: '',
  gender: null,
  currentPassword: '',
})

const replacesIdentifiers = computed(() => {
  return (
    editForm.email.trim().toLowerCase() !== (user.value?.email ?? '').toLowerCase() ||
    editForm.phone.trim() !== (user.value?.phone ?? '')
  )
})
const passwordMissing = computed(() => replacesIdentifiers.value && !editForm.currentPassword)

function openEdit(): void {
  editSubmitted.value = false
  editError.value = ''
  editForm.firstName = user.value?.firstName ?? ''
  editForm.lastName = user.value?.lastName ?? ''
  editForm.email = user.value?.email ?? ''
  editForm.phone = user.value?.phone ?? ''
  editForm.birthDate = toDateInput(user.value?.birthDate)
  editForm.gender = user.value?.gender ?? null
  editForm.currentPassword = ''
  editVisible.value = true
}

function saveEdit(): void {
  editSubmitted.value = true
  editError.value = ''
  const gender = editForm.gender
  if (!gender || passwordMissing.value) return
  const request: UpdateProfileInput = {
    firstName: editForm.firstName.trim(),
    lastName: editForm.lastName.trim(),
    email: editForm.email.trim(),
    phone: editForm.phone.trim(),
    birthDate: editForm.birthDate,
    gender,
    currentPassword: replacesIdentifiers.value ? editForm.currentPassword : null,
  }
  updateProfile.mutate(request, {
    onSuccess: () => {
      editVisible.value = false
      feedback.success(
        t('features.account.profile.savedDetail'),
        t('features.account.profile.savedSummary'),
      )
    },
    onError: (error) => {
      if (error instanceof ApiError && error.code === 'UserCurrentPasswordIncorrect') {
        editError.value = getErrorMessage(error)
        return
      }
      feedback.error(error)
    },
  })
}

const passwordVisible = ref(false)
const passwordForm = reactive({ current: '', next: '', confirm: '' })
const passwordError = ref('')

function openPassword(): void {
  passwordForm.current = ''
  passwordForm.next = ''
  passwordForm.confirm = ''
  passwordError.value = ''
  passwordVisible.value = true
}

function savePassword(): void {
  passwordError.value = ''
  if (passwordForm.next.length < 12) {
    passwordError.value = t('validation.newPasswordMin')
    return
  }
  if (passwordForm.next !== passwordForm.confirm) {
    passwordError.value = t('validation.passwordsMismatch')
    return
  }
  changePassword.mutate(
    { currentPassword: passwordForm.current, newPassword: passwordForm.next },
    {
      onSuccess: () => {
        passwordVisible.value = false
        feedback.success(
          t('features.account.profile.passwordUpdatedDetail'),
          t('features.account.profile.passwordUpdatedSummary'),
        )
      },
      onError: () => {
        passwordError.value = t('features.account.profile.passwordChangeFailed')
      },
    },
  )
}
</script>

<template>
  <section class="acc-pane">
    <div class="acc-pane__head">
      <p class="acc-pane__lead">{{ $t('features.account.profile.lead') }}</p>
      <div class="acc-pane__actions">
        <BaseButton variant="ghost" @click="openEdit">{{
          $t('features.account.profile.editData')
        }}</BaseButton>
        <BaseButton variant="ghost" @click="openPassword">{{
          $t('features.account.profile.changePassword')
        }}</BaseButton>
      </div>
    </div>

    <p v-if="profile.isLoading.value" class="acc-pane__state">{{ $t('common.loading') }}</p>
    <dl v-else-if="user" class="acc-info">
      <div class="acc-info__row">
        <dt>{{ $t('common.name') }}</dt>
        <dd>{{ user.firstName }} {{ user.lastName }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.email') }}</dt>
        <dd>{{ user.email || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.phone') }}</dt>
        <dd>{{ user.phone || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.birthDate') }}</dt>
        <dd>{{ formatDate(user.birthDate) }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.gender') }}</dt>
        <dd>{{ user.gender ? genderLabel(user.gender) : '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.status') }}</dt>
        <dd>{{ user.statusName || '—' }}</dd>
      </div>
    </dl>

    <el-dialog
      v-model="editVisible"
      :title="$t('features.account.profile.editDialogHeader')"
      width="min(90vw, 520px)"
      :close-on-click-modal="false"
    >
      <form class="acc-form" @submit.prevent="saveEdit">
        <div class="acc-form__grid">
          <div class="acc-form__field">
            <label for="p-firstname">{{ $t('common.firstName') }}</label>
            <el-input id="p-firstname" v-model="editForm.firstName" :maxlength="120" required />
          </div>
          <div class="acc-form__field">
            <label for="p-lastname">{{ $t('common.lastName') }}</label>
            <el-input id="p-lastname" v-model="editForm.lastName" :maxlength="120" required />
          </div>
          <div class="acc-form__field">
            <label for="p-email">{{ $t('common.email') }}</label>
            <el-input
              id="p-email"
              v-model="editForm.email"
              type="email"
              :maxlength="256"
              required
            />
          </div>
          <div class="acc-form__field">
            <label for="p-phone">{{ $t('common.phone') }}</label>
            <el-input id="p-phone" v-model="editForm.phone" type="tel" :maxlength="40" required />
          </div>
          <div class="acc-form__field">
            <label for="p-dob">{{ $t('common.birthDate') }}</label>
            <input
              id="p-dob"
              v-model="editForm.birthDate"
              type="date"
              class="acc-date"
              :max="maxBirthDateIso"
              required
            />
          </div>
          <div class="acc-form__field">
            <label for="p-gender">{{ $t('common.gender') }}</label>
            <el-select
              id="p-gender"
              v-model="editForm.gender"
              :class="{ 'ca-invalid': editSubmitted && !editForm.gender }"
            >
              <el-option
                v-for="option in genders"
                :key="option.value"
                :label="option.label"
                :value="option.value"
              />
            </el-select>
            <small v-if="editSubmitted && !editForm.gender" class="acc-form__error">{{
              $t('validation.genderRequired')
            }}</small>
          </div>
          <div v-if="replacesIdentifiers" class="acc-form__field acc-form__field--wide">
            <label for="p-current">{{
              $t('features.account.profile.identifierChange.passwordLabel')
            }}</label>
            <p class="acc-form__hint">
              {{ $t('features.account.profile.identifierChange.message') }}
            </p>
            <el-input
              id="p-current"
              v-model="editForm.currentPassword"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="128"
            />
            <small v-if="editSubmitted && passwordMissing" class="acc-form__error">{{
              $t('features.account.profile.identifierChange.passwordRequired')
            }}</small>
            <small v-else-if="editError" class="acc-form__error">{{ editError }}</small>
          </div>
        </div>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="editVisible = false">{{
            $t('common.cancel')
          }}</BaseButton>
          <BaseButton variant="primary" type="submit" :loading="updateProfile.isPending.value">
            {{ $t('common.save') }}
          </BaseButton>
        </div>
      </form>
    </el-dialog>

    <el-dialog
      v-model="passwordVisible"
      :title="$t('features.account.profile.changePassword')"
      width="min(90vw, 460px)"
      :close-on-click-modal="false"
    >
      <form class="acc-form" @submit.prevent="savePassword">
        <div class="acc-form__field">
          <label for="p-cur">{{ $t('features.account.profile.currentPassword') }}</label>
          <el-input
            id="p-cur"
            v-model="passwordForm.current"
            type="password"
            show-password
            required
          />
        </div>
        <div class="acc-form__field">
          <label for="p-new">{{ $t('features.account.profile.newPassword') }}</label>
          <el-input id="p-new" v-model="passwordForm.next" type="password" show-password required />
        </div>
        <div class="acc-form__field">
          <label for="p-conf">{{ $t('features.account.profile.confirmNewPassword') }}</label>
          <el-input
            id="p-conf"
            v-model="passwordForm.confirm"
            type="password"
            show-password
            required
          />
        </div>
        <p v-if="passwordError" class="acc-form__error">{{ passwordError }}</p>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="passwordVisible = false">
            {{ $t('common.cancel') }}
          </BaseButton>
          <BaseButton variant="primary" type="submit" :loading="changePassword.isPending.value">
            {{ $t('common.save') }}
          </BaseButton>
        </div>
      </form>
    </el-dialog>
  </section>
</template>

<style scoped>
.acc-pane__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 18px;
}

.acc-pane__lead {
  flex: 1;
  min-width: 240px;
  font-size: 14px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  max-width: 46ch;
}

.acc-pane__actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.acc-pane__state {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.acc-info {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px 24px;
}

.acc-info__row {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.acc-info__row dt {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.acc-info__row dd {
  margin: 0;
  font-weight: 600;
  color: var(--ca-text);
  overflow-wrap: anywhere;
}

.acc-form__grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
}

.acc-form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 14px;
}

.acc-form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.acc-date {
  width: 100%;
  background: var(--ca-input-bg);
  color: var(--ca-text);
  border: 1px solid var(--ca-border-strong);
  border-radius: 10px;
  padding: 11px 13px;
  font-family: inherit;
  font-size: 15px;
  outline: none;
}

.acc-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 8px;
}

.acc-form__error {
  color: var(--ca-danger-ink);
  font-size: 13.5px;
  margin: 0 0 10px;
}

.acc-form__field--wide {
  grid-column: 1 / -1;
}

.acc-form__hint {
  margin: 0;
  font-size: 12.5px;
  line-height: 1.4;
  color: var(--ca-text-muted);
}

.ca-invalid :deep(.el-select__wrapper) {
  box-shadow: 0 0 0 1px var(--ca-danger) inset;
}

@media (max-width: 640px) {
  .acc-info,
  .acc-form__grid {
    grid-template-columns: 1fr;
  }

  .acc-pane__actions {
    width: 100%;
  }

  .acc-pane__actions > * {
    flex: 1 1 100%;
  }
}
</style>
