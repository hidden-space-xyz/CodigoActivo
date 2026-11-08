<script setup lang="ts">
import { fullName, GENDERS, genderLabelKey, NationalIdInput } from '@/entities/user'
import {
  EMAIL_ADDRESS_MAX_LENGTH,
  PASSWORD_MAX_LENGTH,
  PERSON_NAME_MAX_LENGTH,
  PHONE_MAX_LENGTH,
  TWO_FACTOR_CODE_MAX_LENGTH,
} from '@/shared/config'
import { BrandButton } from '@/shared/ui/brand-button'

import { useAccount } from '../model/use-account'
import { usePasswordChange } from '../model/use-password-change'
import { useProfileEditor } from '../model/use-profile-editor'

const account = useAccount()
const { profile, updateProfile, changePassword } = account
const {
  user,
  visible: editVisible,
  error: editError,
  form: {
    draft,
    currentPassword,
    submitted: editSubmitted,
    errors,
    requiresPassword,
    passwordMissing,
  },
  isAuthenticator,
  code,
  codeError,
  codeSent,
  requiresCode,
  codeMissing,
  requestCode,
  open: openEdit,
  sendCode,
  save: saveEdit,
} = useProfileEditor(account)
const {
  visible: passwordVisible,
  form: passwordForm,
  error: passwordError,
  open: openPassword,
  save: savePassword,
} = usePasswordChange(account)
</script>

<template>
  <section class="acc-pane" data-testid="profile-section">
    <div class="acc-pane__head">
      <p class="acc-pane__lead">{{ $t('pages.account.profile.lead') }}</p>
      <div class="acc-pane__actions">
        <BrandButton variant="ghost" data-testid="profile-edit-data" @click="openEdit">{{
          $t('pages.account.profile.editData')
        }}</BrandButton>
        <BrandButton variant="ghost" data-testid="profile-change-password" @click="openPassword">{{
          $t('pages.account.profile.changePassword')
        }}</BrandButton>
      </div>
    </div>

    <p v-if="profile.isLoading.value" class="acc-pane__state">{{ $t('common.loading') }}</p>
    <dl v-else-if="user" class="acc-info">
      <div class="acc-info__row">
        <dt>{{ $t('common.name') }}</dt>
        <dd data-testid="profile-value-name">{{ fullName(user) }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.status') }}</dt>
        <dd data-testid="profile-value-status">{{ user.statusName || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.nationalId') }}</dt>
        <dd data-testid="profile-value-national-id">{{ user.nationalId || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.gender') }}</dt>
        <dd data-testid="profile-value-gender">{{ $t(genderLabelKey(user.gender)) }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.phone') }}</dt>
        <dd data-testid="profile-value-phone">{{ user.phone || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.secondaryPhone') }}</dt>
        <dd data-testid="profile-value-secondary-phone">{{ user.secondaryPhone || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.email') }}</dt>
        <dd data-testid="profile-value-email">{{ user.email || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>{{ $t('common.newsConsent') }}</dt>
        <dd data-testid="profile-value-news-consent">
          {{ user.newsConsent ? $t('common.yes') : $t('common.no') }}
        </dd>
      </div>
    </dl>

    <el-dialog
      v-model="editVisible"
      data-testid="profile-dialog"
      :title="$t('pages.account.profile.editDialogHeader')"
      width="min(90vw, 520px)"
      :close-on-click-modal="false"
    >
      <form class="acc-form" @submit.prevent="saveEdit">
        <div class="ca-form-grid">
          <div class="ca-field acc-form__field">
            <label class="ca-field__label" for="p-firstname">{{ $t('common.firstName') }}</label>
            <el-input
              id="p-firstname"
              v-model="draft.firstName"
              data-testid="profile-dialog-first-name"
              :maxlength="PERSON_NAME_MAX_LENGTH"
              :class="{ 'ca-invalid': errors.firstName }"
              :aria-invalid="!!errors.firstName"
              :aria-describedby="errors.firstName ? 'p-firstname-error' : undefined"
              required
            />
            <small v-if="errors.firstName" id="p-firstname-error" class="ca-field__error">{{
              errors.firstName
            }}</small>
          </div>
          <div class="ca-field acc-form__field">
            <label class="ca-field__label" for="p-lastname">{{ $t('common.lastName') }}</label>
            <el-input
              id="p-lastname"
              v-model="draft.lastName"
              data-testid="profile-dialog-last-name"
              :maxlength="PERSON_NAME_MAX_LENGTH"
              :class="{ 'ca-invalid': errors.lastName }"
              :aria-invalid="!!errors.lastName"
              :aria-describedby="errors.lastName ? 'p-lastname-error' : undefined"
              required
            />
            <small v-if="errors.lastName" id="p-lastname-error" class="ca-field__error">{{
              errors.lastName
            }}</small>
          </div>
          <div class="ca-field acc-form__field">
            <label class="ca-field__label" for="p-national-id">{{ $t('common.nationalId') }}</label>
            <NationalIdInput
              id="p-national-id"
              v-model="draft.nationalId"
              data-testid="profile-dialog-national-id"
              :show-errors="editSubmitted"
            />
          </div>
          <div class="ca-field acc-form__field">
            <label class="ca-field__label" for="p-gender">{{ $t('common.gender') }}</label>
            <el-select
              id="p-gender"
              v-model="draft.gender"
              data-testid="profile-dialog-gender"
              :class="{ 'ca-invalid': errors.gender }"
            >
              <el-option
                v-for="gender in GENDERS"
                :key="gender"
                :label="$t(genderLabelKey(gender))"
                :value="gender"
                :data-testid="`option-${gender}`"
              />
            </el-select>
            <small v-if="errors.gender" class="ca-field__error">{{ errors.gender }}</small>
          </div>
          <div class="ca-field acc-form__field">
            <label class="ca-field__label" for="p-phone">{{ $t('common.phone') }}</label>
            <el-input
              id="p-phone"
              v-model="draft.phone"
              data-testid="profile-dialog-phone"
              type="tel"
              :maxlength="PHONE_MAX_LENGTH"
              :class="{ 'ca-invalid': errors.phone }"
              :aria-invalid="!!errors.phone"
              :aria-describedby="errors.phone ? 'p-phone-error' : undefined"
              required
            />
            <small v-if="errors.phone" id="p-phone-error" class="ca-field__error">{{
              errors.phone
            }}</small>
          </div>
          <div class="ca-field acc-form__field">
            <label class="ca-field__label" for="p-secondary-phone">{{
              $t('common.secondaryPhoneOptional')
            }}</label>
            <el-input
              id="p-secondary-phone"
              v-model="draft.secondaryPhone"
              type="tel"
              :maxlength="PHONE_MAX_LENGTH"
              :class="{ 'ca-invalid': errors.secondaryPhone }"
              :aria-invalid="!!errors.secondaryPhone"
              :aria-describedby="errors.secondaryPhone ? 'p-secondary-phone-error' : undefined"
            />
            <small
              v-if="errors.secondaryPhone"
              id="p-secondary-phone-error"
              class="ca-field__error"
              >{{ errors.secondaryPhone }}</small
            >
          </div>
          <div class="ca-field ca-field--wide acc-form__field">
            <label class="ca-field__label" for="p-email">{{ $t('common.email') }}</label>
            <el-input
              id="p-email"
              v-model="draft.email"
              data-testid="profile-dialog-email"
              type="email"
              :maxlength="EMAIL_ADDRESS_MAX_LENGTH"
              :class="{ 'ca-invalid': errors.email }"
              :aria-invalid="!!errors.email"
              :aria-describedby="errors.email ? 'p-email-error' : undefined"
              required
            />
            <small v-if="errors.email" id="p-email-error" class="ca-field__error">{{
              errors.email
            }}</small>
          </div>
          <div class="acc-form__consent ca-field--wide">
            <el-checkbox
              id="p-news-consent"
              v-model="draft.newsConsent"
              data-testid="profile-dialog-news-consent"
              class="ca-checkbox-text acc-form__consent-label"
            >
              {{ $t('common.newsConsentOption') }}
            </el-checkbox>
          </div>
          <div v-if="requiresPassword" class="ca-field ca-field--wide acc-form__field">
            <label class="ca-field__label" for="p-current">{{
              $t('pages.account.profile.contactChange.passwordLabel')
            }}</label>
            <p class="ca-field__hint">
              {{ $t('pages.account.profile.contactChange.message') }}
            </p>
            <el-input
              id="p-current"
              v-model="currentPassword"
              data-testid="profile-dialog-current-password"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="PASSWORD_MAX_LENGTH"
              :aria-invalid="!!editError || (editSubmitted && passwordMissing)"
              :aria-describedby="
                editError || (editSubmitted && passwordMissing) ? 'p-current-error' : undefined
              "
            />
            <small
              v-if="editError"
              id="p-current-error"
              class="ca-field__error"
              data-testid="profile-dialog-current-password-error"
              >{{ editError }}</small
            >
            <small
              v-else-if="editSubmitted && passwordMissing"
              id="p-current-error"
              class="ca-field__error"
              data-testid="profile-dialog-current-password-error"
              >{{ $t('pages.account.profile.contactChange.passwordRequired') }}</small
            >
          </div>
          <div v-if="requiresCode" class="ca-field ca-field--wide acc-form__field">
            <label class="ca-field__label" for="p-code">{{
              $t('pages.account.profile.emailChange.codeLabel')
            }}</label>
            <p class="ca-field__hint" data-testid="profile-dialog-code-hint">
              <template v-if="isAuthenticator">{{
                $t('pages.account.profile.emailChange.authenticatorHint')
              }}</template>
              <template v-else-if="codeSent">{{
                $t('pages.account.profile.emailChange.codeSent', { email: user?.email })
              }}</template>
              <template v-else>{{
                $t('pages.account.profile.emailChange.emailHint', { email: user?.email })
              }}</template>
            </p>
            <div class="acc-form__code">
              <el-input
                id="p-code"
                v-model="code"
                data-testid="profile-dialog-code"
                inputmode="numeric"
                autocomplete="one-time-code"
                :maxlength="TWO_FACTOR_CODE_MAX_LENGTH"
                :aria-invalid="!!codeError || (editSubmitted && codeMissing)"
                :aria-describedby="
                  codeError || (editSubmitted && codeMissing) ? 'p-code-error' : undefined
                "
              />
              <BrandButton
                v-if="!isAuthenticator"
                variant="ghost"
                data-testid="profile-dialog-send-code"
                :loading="requestCode.isPending.value"
                @click="sendCode"
              >
                {{
                  codeSent
                    ? $t('pages.account.profile.emailChange.resend')
                    : $t('pages.account.profile.emailChange.sendCode')
                }}
              </BrandButton>
            </div>
            <small
              v-if="codeError"
              id="p-code-error"
              class="ca-field__error"
              data-testid="profile-dialog-code-error"
              >{{ codeError }}</small
            >
            <small
              v-else-if="editSubmitted && codeMissing"
              id="p-code-error"
              class="ca-field__error"
              data-testid="profile-dialog-code-error"
              >{{ $t('pages.account.profile.emailChange.codeRequired') }}</small
            >
          </div>
        </div>
        <div class="acc-form__actions">
          <BrandButton
            variant="link"
            type="button"
            data-testid="profile-dialog-cancel"
            @click="editVisible = false"
            >{{ $t('common.cancel') }}</BrandButton
          >
          <BrandButton
            variant="primary"
            type="submit"
            :loading="updateProfile.isPending.value"
            data-testid="profile-dialog-save"
          >
            {{ $t('common.save') }}
          </BrandButton>
        </div>
      </form>
    </el-dialog>

    <el-dialog
      v-model="passwordVisible"
      data-testid="password-dialog"
      :title="$t('pages.account.profile.changePassword')"
      width="min(90vw, 460px)"
      :close-on-click-modal="false"
    >
      <form class="acc-form" @submit.prevent="savePassword">
        <div class="ca-field acc-form__field">
          <label class="ca-field__label" for="p-cur">{{
            $t('pages.account.profile.currentPassword')
          }}</label>
          <el-input
            id="p-cur"
            v-model="passwordForm.current"
            data-testid="password-dialog-current"
            type="password"
            show-password
            autocomplete="current-password"
            :maxlength="PASSWORD_MAX_LENGTH"
            required
          />
        </div>
        <div class="ca-field acc-form__field">
          <label class="ca-field__label" for="p-new">{{
            $t('pages.account.profile.newPassword')
          }}</label>
          <el-input
            id="p-new"
            v-model="passwordForm.next"
            data-testid="password-dialog-new"
            type="password"
            show-password
            autocomplete="new-password"
            :maxlength="PASSWORD_MAX_LENGTH"
            required
          />
        </div>
        <div class="ca-field acc-form__field">
          <label class="ca-field__label" for="p-conf">{{
            $t('pages.account.profile.confirmNewPassword')
          }}</label>
          <el-input
            id="p-conf"
            v-model="passwordForm.confirm"
            data-testid="password-dialog-confirm"
            type="password"
            show-password
            autocomplete="new-password"
            :maxlength="PASSWORD_MAX_LENGTH"
            required
          />
        </div>
        <p
          v-if="passwordError"
          class="ca-form-alert acc-form__alert"
          role="alert"
          data-testid="password-dialog-error"
        >
          {{ passwordError }}
        </p>
        <div class="acc-form__actions">
          <BrandButton variant="link" type="button" @click="passwordVisible = false">
            {{ $t('common.cancel') }}
          </BrandButton>
          <BrandButton
            variant="primary"
            type="submit"
            :loading="changePassword.isPending.value"
            data-testid="password-dialog-save"
          >
            {{ $t('common.save') }}
          </BrandButton>
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

.acc-form__field {
  margin-bottom: 14px;
}

.acc-form__consent {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  margin-bottom: 14px;
}

.acc-form__consent-label {
  font-size: 13px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  cursor: pointer;
}

.acc-form__code {
  display: flex;
  gap: 8px;
  align-items: center;
}

.acc-form__code > :first-child {
  flex: 1;
}

.acc-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 8px;
}

.acc-form__alert {
  margin-bottom: 10px;
}

@media (max-width: 640px) {
  .acc-info {
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
