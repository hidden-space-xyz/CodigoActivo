<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'

import { AppButton as Button } from '@/shared/ui'
import { formatFileSize, useActionConfirm } from '@/shared/lib'
import type { SendEmailPayload } from '../model/useSendEmail'
import { MAX_ATTACHMENTS, MAX_ATTACHMENTS_BYTES } from '../model/useSendEmail'

const SUBJECT_MAX_LENGTH = 200
const BODY_MAX_LENGTH = 10000

const props = defineProps<{
  visible: boolean
  target: string
  sending: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [payload: SendEmailPayload]
}>()

const { t } = useI18n()
const { confirmAction } = useActionConfirm()

const subject = ref('')
const body = ref('')
const attachments = ref<File[]>([])
const submitted = ref(false)
const attachmentError = ref('')
const fileInput = ref<HTMLInputElement | null>(null)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    subject.value = ''
    body.value = ''
    attachments.value = []
    submitted.value = false
    attachmentError.value = ''
  },
)

const subjectInvalid = computed(() => subject.value.trim() === '')
const bodyInvalid = computed(() => body.value.trim() === '')

const totalBytes = computed(() => attachments.value.reduce((sum, file) => sum + file.size, 0))

function pick(): void {
  fileInput.value?.click()
}

function onFilesPicked(event: Event): void {
  const input = event.target as HTMLInputElement
  const picked = Array.from(input.files ?? [])
  input.value = ''
  attachmentError.value = ''
  if (picked.length === 0) return

  const merged = [...attachments.value, ...picked]
  if (merged.length > MAX_ATTACHMENTS) {
    attachmentError.value = t('features.sendEmail.attachments.tooMany', {
      max: MAX_ATTACHMENTS,
    })
    return
  }

  const size = merged.reduce((sum, file) => sum + file.size, 0)
  if (size > MAX_ATTACHMENTS_BYTES) {
    attachmentError.value = t('features.sendEmail.attachments.tooLarge', {
      max: formatFileSize(MAX_ATTACHMENTS_BYTES),
    })
    return
  }

  attachments.value = merged
}

function removeAttachment(index: number): void {
  attachments.value = attachments.value.filter((_, position) => position !== index)
  attachmentError.value = ''
}

function close(): void {
  emit('update:visible', false)
}

function send(): void {
  if (props.sending) return

  submitted.value = true
  if (subjectInvalid.value || bodyInvalid.value) return

  confirmAction({
    header: t('features.sendEmail.confirm.header'),
    message: t('features.sendEmail.confirm.message', { target: props.target }),
    acceptLabel: t('features.sendEmail.send'),
    accept: () =>
      emit('submit', {
        subject: subject.value,
        body: body.value,
        attachments: attachments.value,
      }),
  })
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="$t('features.sendEmail.header')"
    :style="{ width: '560px' }"
    @update:visible="close"
  >
    <p class="target">{{ $t('features.sendEmail.target', { target }) }}</p>

    <form class="form" @submit.prevent="send">
      <div class="form__field">
        <label>{{ $t('features.sendEmail.subject') }}</label>
        <InputText
          v-model="subject"
          :maxlength="SUBJECT_MAX_LENGTH"
          :invalid="submitted && subjectInvalid"
          :placeholder="$t('features.sendEmail.subjectPlaceholder')"
          fluid
        />
        <small v-if="submitted && subjectInvalid" class="form__error">
          {{ $t('features.sendEmail.subjectRequired') }}
        </small>
      </div>

      <div class="form__field">
        <label>{{ $t('features.sendEmail.body') }}</label>
        <Textarea
          v-model="body"
          :maxlength="BODY_MAX_LENGTH"
          :invalid="submitted && bodyInvalid"
          :placeholder="$t('features.sendEmail.bodyPlaceholder')"
          rows="9"
          auto-resize
          fluid
        />
        <small v-if="submitted && bodyInvalid" class="form__error">
          {{ $t('features.sendEmail.bodyRequired') }}
        </small>
        <small v-else class="form__hint">{{ $t('features.sendEmail.bodyHint') }}</small>
      </div>

      <div class="form__field">
        <label>{{ $t('features.sendEmail.attachments.label') }}</label>
        <div class="attachments">
          <Button
            :label="$t('features.sendEmail.attachments.add')"
            icon="pi pi-paperclip"
            severity="secondary"
            outlined
            size="small"
            type="button"
            :disabled="sending || attachments.length >= MAX_ATTACHMENTS"
            @click="pick"
          />
          <span v-if="attachments.length > 0" class="attachments__summary">
            {{
              $t(
                'features.sendEmail.attachments.summary',
                { count: attachments.length, size: formatFileSize(totalBytes) },
                attachments.length,
              )
            }}
          </span>
        </div>
        <input
          ref="fileInput"
          type="file"
          multiple
          class="attachments__input"
          @change="onFilesPicked"
        />
        <ul v-if="attachments.length > 0" class="attachments__list">
          <li v-for="(file, index) in attachments" :key="`${file.name}-${index}`">
            <i class="pi pi-file" aria-hidden="true" />
            <span class="attachments__name">{{ file.name }}</span>
            <span class="attachments__size">{{ formatFileSize(file.size) }}</span>
            <Button
              icon="pi pi-times"
              text
              rounded
              size="small"
              type="button"
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
      <Button
        :label="$t('common.cancel')"
        text
        severity="secondary"
        :disabled="sending"
        @click="close"
      />
      <Button
        :label="$t('features.sendEmail.send')"
        icon="pi pi-send"
        :loading="sending"
        @click="send"
      />
    </template>
  </Dialog>
</template>

<style scoped>
.target {
  font-size: 13.5px;
  color: var(--ca-text-muted);
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

.form__field label {
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

.attachments__list i {
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
