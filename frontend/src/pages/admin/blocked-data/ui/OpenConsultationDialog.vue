<script setup lang="ts">
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import {
  BLOCKED_DATA_REFERENCE_MAX_LENGTH,
  PASSWORD_MAX_LENGTH,
  TWO_FACTOR_CODE_MAX_LENGTH,
} from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'

import { AUTHORITIES, authorityLabelKey } from '../lib/labels'
import {
  readConsultationCodeDraft,
  readConsultationRequestDraft,
  toConsultationCodeDraft,
  toConsultationRequestDraft,
} from '../model/open-consultation-form'
import type { ConsultationRequest } from '../model/types'

/** Opens the dialog; what was typed is discarded whenever it opens or closes. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Shows the second step, which asks for the code, instead of the request and the password. */
  codeStep: boolean
  /** The account reads its code from an authenticator application instead of its email. */
  usesAuthenticator: boolean
  /** Address the code is emailed to. */
  email: string
  /** Shows a loading state while the code is emailed. */
  sendingCode: boolean
  /** Shows a loading state on the confirm button while the consultation opens. */
  saving: boolean
  /** Rejection reported by the API, such as an incorrect password or code; empty hides it. */
  error: string
}>()

const emit = defineEmits<{
  /** Fired with a complete request, ready to ask for its code. */
  continue: [request: ConsultationRequest]
  /** Fired to email another code. */
  resend: []
  /** Fired with the code, ready to open the consultation. */
  submit: [code: string]
}>()

const { t } = useI18n()
const requestForm = useForm({
  initial: toConsultationRequestDraft,
  read: readConsultationRequestDraft,
})
const codeForm = useForm({ initial: toConsultationCodeDraft, read: readConsultationCodeDraft })
const { draft, errors, invalid } = requestForm
const { draft: codeDraft, errors: codeErrors, invalid: codeInvalid } = codeForm

const authorityOptions = computed(() =>
  AUTHORITIES.map((authority) => ({ value: authority, label: t(authorityLabelKey(authority)) })),
)

watch(visible, () => {
  requestForm.reset()
  codeForm.reset()
})

function confirm(): void {
  if (props.codeStep) {
    const code = codeForm.submit()
    if (code) emit('submit', code)
    return
  }
  const request = requestForm.submit()
  if (request) emit('continue', request)
}
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="$t('pages.admin.blockedData.open.title')"
    width="520px"
    :close-on-click-modal="false"
  >
    <form class="form" @submit.prevent="confirm">
      <template v-if="!codeStep">
        <p class="form__message">{{ $t('pages.admin.blockedData.open.intro') }}</p>

        <div class="ca-field">
          <label class="ca-field__label" for="blocked-authority">{{
            $t('pages.admin.blockedData.open.authority')
          }}</label>
          <el-select
            id="blocked-authority"
            v-model="draft.authority"
            :placeholder="$t('pages.admin.blockedData.open.authorityPlaceholder')"
            :class="{ 'ca-invalid': invalid('authority') }"
            :aria-invalid="invalid('authority')"
            :aria-describedby="errors.authority ? 'blocked-authority-error' : undefined"
          >
            <el-option
              v-for="option in authorityOptions"
              :key="option.value"
              :label="option.label"
              :value="option.value"
            />
          </el-select>
          <small v-if="errors.authority" id="blocked-authority-error" class="ca-field__error">{{
            errors.authority
          }}</small>
        </div>

        <div class="ca-field">
          <label class="ca-field__label" for="blocked-reference">{{
            $t('pages.admin.blockedData.open.reference')
          }}</label>
          <el-input
            id="blocked-reference"
            v-model="draft.reference"
            :maxlength="BLOCKED_DATA_REFERENCE_MAX_LENGTH"
            :class="{ 'ca-invalid': invalid('reference') }"
            :aria-invalid="invalid('reference')"
            :aria-describedby="errors.reference ? 'blocked-reference-error' : undefined"
          />
          <small v-if="errors.reference" id="blocked-reference-error" class="ca-field__error">{{
            errors.reference
          }}</small>
        </div>

        <div class="ca-field">
          <label class="ca-field__label" for="blocked-password">{{
            $t('pages.admin.blockedData.open.password')
          }}</label>
          <el-input
            id="blocked-password"
            v-model="draft.password"
            type="password"
            show-password
            autocomplete="current-password"
            :maxlength="PASSWORD_MAX_LENGTH"
            :class="{ 'ca-invalid': invalid('password') }"
            :aria-invalid="invalid('password')"
            :aria-describedby="errors.password ? 'blocked-password-error' : undefined"
          />
          <small v-if="errors.password" id="blocked-password-error" class="ca-field__error">{{
            errors.password
          }}</small>
        </div>
      </template>

      <template v-else>
        <p class="form__message">
          {{
            usesAuthenticator
              ? $t('pages.admin.blockedData.open.authenticatorIntro')
              : $t('pages.admin.blockedData.open.codeSent', { email })
          }}
        </p>

        <div class="ca-field">
          <label class="ca-field__label" for="blocked-code">{{
            $t('pages.admin.blockedData.open.code')
          }}</label>
          <el-input
            id="blocked-code"
            v-model="codeDraft.code"
            inputmode="numeric"
            autocomplete="one-time-code"
            :maxlength="TWO_FACTOR_CODE_MAX_LENGTH"
            :class="{ 'ca-invalid': codeInvalid('code') }"
            :aria-invalid="codeInvalid('code')"
            :aria-describedby="codeErrors.code ? 'blocked-code-error' : undefined"
          />
          <small v-if="codeErrors.code" id="blocked-code-error" class="ca-field__error">{{
            codeErrors.code
          }}</small>
        </div>

        <p v-if="!usesAuthenticator" class="form__resend">
          {{ $t('pages.admin.blockedData.open.resendPrompt') }}
          <ActionButton
            :label="$t('pages.admin.blockedData.open.resend')"
            type="primary"
            link
            :loading="sendingCode"
            @click="emit('resend')"
          />
        </p>
      </template>

      <p v-if="error" class="ca-form-alert" role="alert">{{ error }}</p>
    </form>

    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="visible = false" />
      <ActionButton
        v-if="!codeStep"
        :label="
          usesAuthenticator
            ? $t('pages.admin.blockedData.open.continue')
            : $t('pages.admin.blockedData.open.sendCode')
        "
        type="primary"
        :loading="sendingCode"
        @click="confirm"
      />
      <ActionButton
        v-else
        :label="$t('pages.admin.blockedData.open.confirm')"
        type="primary"
        :loading="saving"
        @click="confirm"
      />
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

.form__message {
  margin: 0;
  line-height: 1.5;
  color: var(--ca-text);
}

.form__resend {
  margin: 0;
  line-height: 1.6;
  color: var(--ca-text-muted);
}
</style>
