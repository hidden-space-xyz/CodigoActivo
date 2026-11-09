<script setup lang="ts">
import { fullName, genderLabelKey, PersonFields } from '@/entities/user'
import {
  EMAIL_ADDRESS_MAX_LENGTH,
  PASSWORD_MAX_LENGTH,
  PHONE_MAX_LENGTH,
  TWO_FACTOR_CODE_MAX_LENGTH,
} from '@/shared/config'
import { BrandButton } from '@/shared/ui/brand-button'
import { FormField } from '@/shared/ui/form-field'

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
        <div class="ca-form-grid acc-form__grid">
          <PersonFields
            v-model:first-name="draft.firstName"
            v-model:last-name="draft.lastName"
            v-model:national-id="draft.nationalId"
            v-model:gender="draft.gender"
            id-prefix="p"
            :errors="errors"
            :show-national-id-errors="editSubmitted"
          />
          <FormField
            v-slot="{ control }"
            for="p-phone"
            :label="$t('common.phone')"
            :error="errors.phone"
          >
            <el-input
              v-model="draft.phone"
              v-bind="control"
              data-testid="profile-dialog-phone"
              type="tel"
              :maxlength="PHONE_MAX_LENGTH"
              required
            />
          </FormField>
          <FormField
            v-slot="{ control }"
            for="p-secondary-phone"
            :label="$t('common.secondaryPhoneOptional')"
            :error="errors.secondaryPhone"
          >
            <el-input
              v-model="draft.secondaryPhone"
              v-bind="control"
              type="tel"
              :maxlength="PHONE_MAX_LENGTH"
            />
          </FormField>
          <FormField
            v-slot="{ control }"
            for="p-email"
            :label="$t('common.email')"
            :error="errors.email"
            wide
          >
            <el-input
              v-model="draft.email"
              v-bind="control"
              data-testid="profile-dialog-email"
              type="email"
              :maxlength="EMAIL_ADDRESS_MAX_LENGTH"
              required
            />
          </FormField>
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
          <FormField
            v-if="requiresPassword"
            v-slot="{ control }"
            for="p-current"
            :label="$t('pages.account.profile.contactChange.passwordLabel')"
            :error="
              editError ||
              (editSubmitted && passwordMissing
                ? $t('pages.account.profile.contactChange.passwordRequired')
                : null)
            "
            error-testid="profile-dialog-current-password-error"
            wide
          >
            <p class="ca-field__hint">
              {{ $t('pages.account.profile.contactChange.message') }}
            </p>
            <el-input
              v-model="currentPassword"
              v-bind="control"
              data-testid="profile-dialog-current-password"
              type="password"
              show-password
              autocomplete="current-password"
              :maxlength="PASSWORD_MAX_LENGTH"
            />
          </FormField>
          <FormField
            v-if="requiresCode"
            v-slot="{ control }"
            for="p-code"
            :label="$t('common.verificationCode')"
            :error="
              codeError ||
              (editSubmitted && codeMissing
                ? $t('pages.account.profile.emailChange.codeRequired')
                : null)
            "
            error-testid="profile-dialog-code-error"
            wide
          >
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
                v-model="code"
                v-bind="control"
                data-testid="profile-dialog-code"
                inputmode="numeric"
                autocomplete="one-time-code"
                :maxlength="TWO_FACTOR_CODE_MAX_LENGTH"
              />
              <BrandButton
                v-if="!isAuthenticator"
                variant="ghost"
                data-testid="profile-dialog-send-code"
                :loading="requestCode.isPending.value"
                @click="sendCode"
              >
                {{ codeSent ? $t('common.resendCode') : $t('common.sendCode') }}
              </BrandButton>
            </div>
          </FormField>
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
        <FormField
          for="p-cur"
          :label="$t('pages.account.profile.currentPassword')"
          class="acc-form__field"
        >
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
        </FormField>
        <FormField
          for="p-new"
          :label="$t('pages.account.profile.newPassword')"
          class="acc-form__field"
        >
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
        </FormField>
        <FormField
          for="p-conf"
          :label="$t('pages.account.profile.confirmNewPassword')"
          class="acc-form__field"
        >
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
        </FormField>
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

.acc-form__consent {
  display: flex;
  align-items: flex-start;
  gap: 10px;
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
</style>
