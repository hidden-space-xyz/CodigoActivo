<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'

import { useAccount } from '../model/useAccount'
import type { UpdateProfileInput } from '@/entities/account'
import { useSession } from '@/entities/session'
import { BaseButton } from '@/shared/ui'
import { formatDate, toDateInput, todayIso, useCrudFeedback } from '@/shared/lib'

const { t } = useI18n()
const feedback = useCrudFeedback()
const session = useSession()
const { profile, updateProfile, changePassword, deleteOwnAccount } = useAccount()

const maxBirthDateIso = todayIso()
const user = computed(() => profile.data.value ?? null)

const editVisible = ref(false)
const editForm = reactive<{
  firstName: string
  lastName: string
  email: string
  phone: string
  birthDate: string
}>({ firstName: '', lastName: '', email: '', phone: '', birthDate: '' })

function openEdit(): void {
  editForm.firstName = user.value?.firstName ?? ''
  editForm.lastName = user.value?.lastName ?? ''
  editForm.email = user.value?.email ?? ''
  editForm.phone = user.value?.phone ?? ''
  editForm.birthDate = toDateInput(user.value?.birthDate)
  editVisible.value = true
}

function saveEdit(): void {
  const request: UpdateProfileInput = {
    firstName: editForm.firstName.trim(),
    lastName: editForm.lastName.trim(),
    email: editForm.email.trim(),
    phone: editForm.phone.trim(),
    birthDate: editForm.birthDate,
  }
  updateProfile.mutate(request, {
    onSuccess: () => {
      editVisible.value = false
      feedback.success(
        t('features.account.profile.savedDetail'),
        t('features.account.profile.savedSummary'),
      )
    },
    onError: (error) => feedback.error(error),
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
  if (passwordForm.next.length < 8) {
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

const deleteVisible = ref(false)

function confirmDeleteAccount(): void {
  deleteOwnAccount.mutate(undefined, {
    onError: (error) => feedback.error(error),
  })
}
</script>

<template>
  <section class="acc-card">
    <div class="acc-card__head">
      <h2 class="acc-card__title">{{ $t('features.account.profile.title') }}</h2>
      <div class="acc-card__actions">
        <BaseButton variant="ghost" @click="openEdit">{{
          $t('features.account.profile.editData')
        }}</BaseButton>
        <BaseButton variant="ghost" @click="openPassword">{{
          $t('features.account.profile.changePassword')
        }}</BaseButton>
        <BaseButton v-if="!session.isAdmin" variant="ghost" @click="deleteVisible = true">
          {{ $t('features.account.profile.deleteAccount') }}
        </BaseButton>
      </div>
    </div>

    <p v-if="profile.isLoading.value" class="acc-card__state">{{ $t('common.loading') }}</p>
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
        <dt>{{ $t('common.status') }}</dt>
        <dd>{{ user.statusName || '—' }}</dd>
      </div>
    </dl>

    <Dialog
      v-model:visible="editVisible"
      modal
      :header="$t('features.account.profile.editDialogHeader')"
      :style="{ width: '90vw', maxWidth: '520px' }"
    >
      <form class="acc-form" @submit.prevent="saveEdit">
        <div class="acc-form__grid">
          <div class="acc-form__field">
            <label for="p-firstname">{{ $t('common.firstName') }}</label>
            <InputText
              id="p-firstname"
              v-model="editForm.firstName"
              :maxlength="120"
              required
              fluid
            />
          </div>
          <div class="acc-form__field">
            <label for="p-lastname">{{ $t('common.lastName') }}</label>
            <InputText
              id="p-lastname"
              v-model="editForm.lastName"
              :maxlength="120"
              required
              fluid
            />
          </div>
          <div class="acc-form__field">
            <label for="p-email">{{ $t('common.email') }}</label>
            <InputText
              id="p-email"
              v-model="editForm.email"
              type="email"
              :maxlength="256"
              required
              fluid
            />
          </div>
          <div class="acc-form__field">
            <label for="p-phone">{{ $t('common.phone') }}</label>
            <InputText
              id="p-phone"
              v-model="editForm.phone"
              type="tel"
              :maxlength="40"
              required
              fluid
            />
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
    </Dialog>

    <Dialog
      v-model:visible="passwordVisible"
      modal
      :header="$t('features.account.profile.changePassword')"
      :style="{ width: '90vw', maxWidth: '460px' }"
    >
      <form class="acc-form" @submit.prevent="savePassword">
        <div class="acc-form__field">
          <label for="p-cur">{{ $t('features.account.profile.currentPassword') }}</label>
          <Password
            input-id="p-cur"
            v-model="passwordForm.current"
            :feedback="false"
            toggle-mask
            required
            fluid
          />
        </div>
        <div class="acc-form__field">
          <label for="p-new">{{ $t('features.account.profile.newPassword') }}</label>
          <Password
            input-id="p-new"
            v-model="passwordForm.next"
            :feedback="false"
            toggle-mask
            required
            fluid
          />
        </div>
        <div class="acc-form__field">
          <label for="p-conf">{{ $t('features.account.profile.confirmNewPassword') }}</label>
          <Password
            input-id="p-conf"
            v-model="passwordForm.confirm"
            :feedback="false"
            toggle-mask
            required
            fluid
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
    </Dialog>

    <Dialog
      v-model:visible="deleteVisible"
      modal
      :header="$t('features.account.profile.deleteAccount')"
      :style="{ width: '90vw', maxWidth: '460px' }"
    >
      <p class="acc-confirm">
        {{ $t('features.account.profile.deleteConfirm') }}
      </p>
      <div class="acc-form__actions">
        <BaseButton variant="link" type="button" @click="deleteVisible = false">{{
          $t('common.cancel')
        }}</BaseButton>
        <BaseButton
          variant="primary"
          :loading="deleteOwnAccount.isPending.value"
          @click="confirmDeleteAccount"
        >
          {{ $t('common.delete') }}
        </BaseButton>
      </div>
    </Dialog>
  </section>
</template>

<style scoped>
.acc-card {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 26px 28px;
}

.acc-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 18px;
}

.acc-card__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 20px;
  color: var(--ca-text-bright);
}

.acc-card__actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.acc-card__state {
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
}

.acc-info__row dt {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.acc-info__row dd {
  margin: 0;
  font-weight: 600;
  color: var(--ca-text);
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
  color-scheme: dark;
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

.acc-confirm {
  color: var(--ca-text);
  line-height: 1.6;
  margin: 0 0 16px;
}

@media (max-width: 620px) {
  .acc-info,
  .acc-form__grid {
    grid-template-columns: 1fr;
  }
}
</style>
