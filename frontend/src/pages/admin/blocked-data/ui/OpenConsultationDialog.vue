<script setup lang="ts">
import { watch } from 'vue'

import {
  BLOCKED_DATA_REFERENCE_MAX_LENGTH,
  BLOCKED_DATA_REQUESTER_MAX_LENGTH,
  PASSWORD_MAX_LENGTH,
  TWO_FACTOR_CODE_MAX_LENGTH,
} from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'
import { FormField } from '@/shared/ui/form-field'

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

const requestForm = useForm({
  initial: toConsultationRequestDraft,
  read: readConsultationRequestDraft,
})
const codeForm = useForm({ initial: toConsultationCodeDraft, read: readConsultationCodeDraft })
const { draft, errors, invalid } = requestForm
const { draft: codeDraft, errors: codeErrors, invalid: codeInvalid } = codeForm

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
    data-testid="blocked-data-dialog"
    :title="$t('pages.admin.blockedData.open.title')"
    width="520px"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
  >
    <form class="form" @submit.prevent="confirm">
      <template v-if="!codeStep">
        <p class="form__message">{{ $t('pages.admin.blockedData.open.intro') }}</p>

        <FormField
          v-slot="{ control }"
          for="blocked-requester"
          :label="$t('pages.admin.blockedData.open.requester')"
          :invalid="invalid('requester')"
          :error="errors.requester"
        >
          <el-input
            v-model="draft.requester"
            v-bind="control"
            data-testid="blocked-data-dialog-requester"
            :maxlength="BLOCKED_DATA_REQUESTER_MAX_LENGTH"
          />
        </FormField>

        <FormField
          v-slot="{ control }"
          for="blocked-reference"
          :label="$t('pages.admin.blockedData.open.reference')"
          :invalid="invalid('reference')"
          :error="errors.reference"
        >
          <el-input
            v-model="draft.reference"
            v-bind="control"
            data-testid="blocked-data-dialog-reference"
            :maxlength="BLOCKED_DATA_REFERENCE_MAX_LENGTH"
          />
        </FormField>

        <FormField
          v-slot="{ control }"
          for="blocked-password"
          :label="$t('common.yourPassword')"
          :invalid="invalid('password')"
          :error="errors.password"
        >
          <el-input
            v-model="draft.password"
            v-bind="control"
            data-testid="blocked-data-dialog-password"
            type="password"
            show-password
            autocomplete="current-password"
            :maxlength="PASSWORD_MAX_LENGTH"
          />
        </FormField>
      </template>

      <template v-else>
        <p class="form__message">
          {{
            usesAuthenticator
              ? $t('pages.admin.blockedData.open.authenticatorIntro')
              : $t('pages.admin.blockedData.open.codeSent', { email })
          }}
        </p>

        <FormField
          v-slot="{ control }"
          for="blocked-code"
          :label="$t('common.verificationCode')"
          :invalid="codeInvalid('code')"
          :error="codeErrors.code"
        >
          <el-input
            v-model="codeDraft.code"
            v-bind="control"
            data-testid="blocked-data-dialog-code"
            inputmode="numeric"
            autocomplete="one-time-code"
            :maxlength="TWO_FACTOR_CODE_MAX_LENGTH"
          />
        </FormField>

        <p v-if="!usesAuthenticator" class="form__resend">
          {{ $t('common.resendPrompt') }}
          <ActionButton
            :label="$t('common.resendCode')"
            type="primary"
            link
            :loading="sendingCode"
            @click="emit('resend')"
          />
        </p>
      </template>

      <p v-if="error" class="ca-form-alert" role="alert" data-testid="blocked-data-dialog-error">
        {{ error }}
      </p>
    </form>

    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="visible = false" />
      <ActionButton
        v-if="!codeStep"
        :label="
          usesAuthenticator ? $t('pages.admin.blockedData.open.continue') : $t('common.sendCode')
        "
        data-testid="blocked-data-dialog-continue"
        type="primary"
        :loading="sendingCode"
        @click="confirm"
      />
      <ActionButton
        v-else
        :label="$t('pages.admin.blockedData.open.confirm')"
        data-testid="blocked-data-dialog-confirm"
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
