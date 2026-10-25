<script setup lang="ts">
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { BLOCKED_DATA_REFERENCE_MAX_LENGTH } from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'

import { AUTHORITIES, authorityLabelKey } from '../lib/labels'
import { readOpenConsultationDraft, toOpenConsultationDraft } from '../model/open-consultation-form'
import type { OpenConsultationInput } from '../model/types'

/** Opens the dialog; what was typed is discarded whenever it opens or closes. */
const visible = defineModel<boolean>('visible', { required: true })

defineProps<{
  /** Shows a loading state on the confirm button while the consultation opens. */
  saving: boolean
  /** Rejection reported by the API, such as an incorrect password or code; empty hides it. */
  error: string
}>()

const emit = defineEmits<{
  /** Fired with a complete request, ready to open the consultation. */
  submit: [input: OpenConsultationInput]
}>()

const { t } = useI18n()
const form = useForm({ initial: toOpenConsultationDraft, read: readOpenConsultationDraft })
const { draft, errors, invalid } = form

const authorityOptions = computed(() =>
  AUTHORITIES.map((authority) => ({ value: authority, label: t(authorityLabelKey(authority)) })),
)

watch(visible, () => {
  form.reset()
})

function confirm(): void {
  const input = form.submit()
  if (input) emit('submit', input)
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
          :maxlength="128"
          :class="{ 'ca-invalid': invalid('password') }"
          :aria-invalid="invalid('password')"
          :aria-describedby="errors.password ? 'blocked-password-error' : undefined"
        />
        <small v-if="errors.password" id="blocked-password-error" class="ca-field__error">{{
          errors.password
        }}</small>
      </div>

      <div class="ca-field">
        <label class="ca-field__label" for="blocked-code">{{
          $t('pages.admin.blockedData.open.code')
        }}</label>
        <el-input
          id="blocked-code"
          v-model="draft.code"
          inputmode="numeric"
          autocomplete="one-time-code"
          :maxlength="16"
          :class="{ 'ca-invalid': invalid('code') || error }"
          :aria-invalid="invalid('code') || !!error"
          :aria-describedby="errors.code || error ? 'blocked-code-error' : undefined"
        />
        <small v-if="errors.code" id="blocked-code-error" class="ca-field__error">{{
          errors.code
        }}</small>
        <small v-else-if="error" id="blocked-code-error" class="ca-field__error">{{ error }}</small>
      </div>
    </form>

    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="visible = false" />
      <ActionButton
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
</style>
