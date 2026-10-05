<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { useActionConfirm } from '@/shared/lib/feedback'
import { useForm } from '@/shared/lib/form'
import { formatFileSize } from '@/shared/lib/number'
import { ActionButton } from '@/shared/ui/action-button'
import { AppIcon } from '@/shared/ui/app-icon'

import {
  addAttachments,
  MAX_ATTACHMENTS,
  MAX_ATTACHMENTS_BYTES,
  readEmailDraft,
  toEmailDraft,
} from '../model/email-form'
import type { SendEmailPayload } from '../model/types'

const SUBJECT_MAX_LENGTH = 200
const BODY_MAX_LENGTH = 10000

const props = defineProps<{
  /** Opens the dialog; each time it opens subject, body and attachments are cleared. */
  visible: boolean
  /** Localized recipient description shown above the form and in the confirmation. */
  target: string
  /** Disables sending and closing while an email is in flight. */
  sending: boolean
  /**
   * Addresses the email will reach, shown and confirmed instead of the selection size; zero blocks
   * sending. `null` while the audience is loading or when it could not be loaded.
   */
  recipients?: number | null
  /**
   * Recipients without promotional consent; above zero shows a non-blocking warning. `null` while
   * the audience is loading or when it could not be loaded, which hides the warning.
   */
  withoutConsent?: number | null
}>()

const emit = defineEmits<{
  /** Fired with `false` when the dialog is closed, unless a send is in flight. */
  'update:visible': [value: boolean]
  /** Fired with the untrimmed subject, body and attachments after the user confirms sending. */
  submit: [payload: SendEmailPayload]
}>()

const { t } = useI18n()
const { confirmAction } = useActionConfirm()

const form = useForm({ initial: toEmailDraft, read: readEmailDraft })
const { draft, errors, invalid } = form
const attachmentError = ref('')
const fileInput = ref<HTMLInputElement | null>(null)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    form.reset()
    attachmentError.value = ''
  },
)

const recipientsWithoutConsent = computed(() => props.withoutConsent ?? 0)
const knownRecipients = computed(() => props.recipients ?? null)
const nobodyReachable = computed(() => knownRecipients.value === 0)

const totalBytes = computed(() => draft.attachments.reduce((sum, file) => sum + file.size, 0))

function pick(): void {
  fileInput.value?.click()
}

function onFilesPicked(event: Event): void {
  const input = event.target as HTMLInputElement
  const picked = Array.from(input.files ?? [])
  input.value = ''
  attachmentError.value = ''
  if (picked.length === 0) return

  const added = addAttachments(draft.attachments, picked)
  if ('problem' in added) {
    attachmentError.value =
      added.problem === 'tooMany'
        ? t('features.sendEmail.attachments.tooMany', { max: MAX_ATTACHMENTS })
        : t('features.sendEmail.attachments.tooLarge', {
            max: formatFileSize(MAX_ATTACHMENTS_BYTES),
          })
    return
  }
  draft.attachments = added.attachments
}

function removeAttachment(index: number): void {
  draft.attachments = draft.attachments.filter((_, position) => position !== index)
  attachmentError.value = ''
}

function close(): void {
  if (props.sending) return
  emit('update:visible', false)
}

function send(): void {
  if (props.sending || nobodyReachable.value) return

  const payload = form.submit()
  if (!payload) return

  const count = knownRecipients.value
  confirmAction({
    header: t('features.sendEmail.confirm.header'),
    message:
      count === null
        ? t('features.sendEmail.confirm.message', { target: props.target })
        : t('features.sendEmail.confirm.messageCount', { count }, count),
    acceptLabel: t('features.sendEmail.send'),
    accept: () => emit('submit', payload),
  })
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="$t('features.sendEmail.header')"
    width="min(92vw, 560px)"
    :show-close="!sending"
    :close-on-press-escape="!sending"
    :close-on-click-modal="false"
    @update:model-value="close"
  >
    <p class="target">{{ $t('features.sendEmail.target', { target }) }}</p>
    <p v-if="knownRecipients !== null" class="target">
      {{ $t('features.sendEmail.recipients', { count: knownRecipients }, knownRecipients) }}
    </p>

    <el-alert
      v-if="recipientsWithoutConsent > 0"
      class="consent-warning"
      type="warning"
      show-icon
      :closable="false"
      :title="
        $t(
          'features.sendEmail.withoutConsentWarning',
          { count: recipientsWithoutConsent },
          recipientsWithoutConsent,
        )
      "
    />

    <form class="form" @submit.prevent="send">
      <div class="form__field">
        <label for="send-email-subject">{{ $t('features.sendEmail.subject') }}</label>
        <el-input
          id="send-email-subject"
          v-model="draft.subject"
          :maxlength="SUBJECT_MAX_LENGTH"
          :class="{ 'ca-invalid': invalid('subject') }"
          :placeholder="$t('features.sendEmail.subjectPlaceholder')"
        />
        <small v-if="errors.subject" class="form__error">{{ errors.subject }}</small>
      </div>

      <div class="form__field">
        <label for="send-email-body">{{ $t('features.sendEmail.body') }}</label>
        <el-input
          id="send-email-body"
          v-model="draft.body"
          type="textarea"
          :maxlength="BODY_MAX_LENGTH"
          :class="{ 'ca-invalid': invalid('body') }"
          :placeholder="$t('features.sendEmail.bodyPlaceholder')"
          :autosize="{ minRows: 6, maxRows: 12 }"
        />
        <small v-if="errors.body" class="form__error">{{ errors.body }}</small>
        <small v-else class="form__hint">{{ $t('features.sendEmail.bodyHint') }}</small>
      </div>

      <div class="form__field">
        <div id="send-email-attachments-label" class="form__label">
          {{ $t('features.sendEmail.attachments.label') }}
        </div>
        <div class="attachments" role="group" aria-labelledby="send-email-attachments-label">
          <ActionButton
            :label="$t('features.sendEmail.attachments.add')"
            icon="paperclip"
            plain
            size="small"
            :disabled="sending || draft.attachments.length >= MAX_ATTACHMENTS"
            @click="pick"
          />
          <span v-if="draft.attachments.length > 0" class="attachments__summary">
            {{
              $t(
                'features.sendEmail.attachments.summary',
                { count: draft.attachments.length, size: formatFileSize(totalBytes) },
                draft.attachments.length,
              )
            }}
          </span>
        </div>
        <input
          ref="fileInput"
          type="file"
          multiple
          class="attachments__input"
          aria-hidden="true"
          tabindex="-1"
          @change="onFilesPicked"
        />
        <ul v-if="draft.attachments.length > 0" class="attachments__list">
          <li v-for="(file, index) in draft.attachments" :key="`${file.name}-${index}`">
            <AppIcon name="file" />
            <span class="attachments__name">{{ file.name }}</span>
            <span class="attachments__size">{{ formatFileSize(file.size) }}</span>
            <ActionButton
              icon="times"
              text
              circle
              size="small"
              :disabled="sending"
              :aria-label="$t('features.sendEmail.attachments.remove')"
              @click="removeAttachment(index)"
            />
          </li>
        </ul>
        <small v-if="attachmentError" class="form__error">{{ attachmentError }}</small>
      </div>
    </form>

    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="sending" @click="close" />
      <ActionButton
        :label="$t('features.sendEmail.send')"
        icon="send"
        type="primary"
        :loading="sending"
        :disabled="nobodyReachable"
        @click="send"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
.target {
  font-size: 13.5px;
  color: var(--ca-text-muted);
  margin: 0 0 14px;
}

.consent-warning {
  margin: 0 0 14px;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-top: 2px;
}

.form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form__field label,
.form__label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.form__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}

.form__hint {
  color: var(--ca-text-dim);
  font-size: 12.5px;
}

.attachments {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.attachments__summary {
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.attachments__input {
  display: none;
}

.attachments__list {
  list-style: none;
  margin: 4px 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.attachments__list li {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  padding: 4px 6px 4px 10px;
  border: 1px solid var(--ca-border-soft);
  border-radius: 8px;
  background: var(--ca-surface);
}

.attachments__list li i {
  font-size: 12px;
  color: var(--ca-text-muted);
}

.attachments__name {
  flex: 1 1 auto;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.attachments__size {
  font-size: 12px;
  color: var(--ca-text-dim);
  white-space: nowrap;
}
</style>
