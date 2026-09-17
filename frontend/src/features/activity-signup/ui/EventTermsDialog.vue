<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'

import { AppButton as Button } from '@/shared/ui'
import RichTextContent from '@/shared/ui/RichTextContent.vue'

import type { TermsDecisionInput } from '@/entities/activity'
import type { EventTermsDocumentState } from '@/entities/event'

const props = defineProps<{
  /** Opens the dialog; every time it opens, decisions reset to unchecked for `documents`. */
  visible: boolean
  /** Pending documents to decide on (already decided ones are not shown). */
  documents: readonly EventTermsDocumentState[]
}>()

const emit = defineEmits<{
  /** Fired with `false` when the dialog is closed or dismissed without confirming. */
  'update:visible': [value: boolean]
  /** Fired with one decision per document in `documents` once every required one is checked. */
  confirm: [decisions: TermsDecisionInput[]]
}>()

const { t } = useI18n()

const checked = reactive<Record<string, boolean>>({})

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    for (const key of Object.keys(checked)) delete checked[key]
    for (const document of props.documents) checked[document.id] = false
  },
)

const missingRequired = computed(() =>
  props.documents.some((document) => document.required && !checked[document.id]),
)

const preview = reactive<{ visible: boolean; document: EventTermsDocumentState | null }>({
  visible: false,
  document: null,
})

/** Opens the document content popup for `document`. */
function openPreview(document: EventTermsDocumentState): void {
  preview.document = document
  preview.visible = true
}

/** Marks the previewed document as accepted and closes the popup. */
function acceptFromPreview(): void {
  if (preview.document) checked[preview.document.id] = true
  preview.visible = false
}

/** Marks the previewed document as rejected and closes the popup. */
function rejectFromPreview(): void {
  if (preview.document) checked[preview.document.id] = false
  preview.visible = false
}

function close(): void {
  emit('update:visible', false)
}

function confirm(): void {
  if (missingRequired.value) return
  emit(
    'confirm',
    props.documents.map((document) => ({
      termsDocumentId: document.id,
      accepted: !!checked[document.id],
    })),
  )
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="t('features.activitySignup.terms.header')"
    width="min(680px, 94vw)"
    append-to-body
    @update:model-value="close"
  >
    <p class="terms-dialog__lead">{{ t('features.activitySignup.terms.lead') }}</p>
    <ul class="terms-dialog__list">
      <li v-for="document in documents" :key="document.id" class="terms-dialog__row">
        <el-checkbox :id="`terms-${document.id}`" v-model="checked[document.id]" />
        <div class="terms-dialog__info">
          <button
            type="button"
            class="terms-dialog__link"
            :title="t('features.activitySignup.terms.openDocument', { name: document.name })"
            @click="openPreview(document)"
          >
            {{ document.name }}
          </button>
          <span
            class="terms-dialog__tag"
            :class="{
              'terms-dialog__tag--required': document.required,
              'terms-dialog__tag--optional': !document.required,
            }"
          >
            {{
              document.required
                ? t('features.activitySignup.terms.required')
                : t('features.activitySignup.terms.optional')
            }}
          </span>
        </div>
      </li>
    </ul>
    <p v-if="missingRequired" class="terms-dialog__warning">
      {{ t('features.activitySignup.terms.missingRequired') }}
    </p>
    <template #footer>
      <Button :label="t('common.cancel')" text @click="close" />
      <Button
        :label="t('features.activitySignup.terms.confirm')"
        type="primary"
        :disabled="missingRequired"
        @click="confirm"
      />
    </template>
  </el-dialog>

  <el-dialog
    v-model="preview.visible"
    :title="preview.document?.name"
    :aria-label="`${t('features.activitySignup.terms.documentHeader')}: ${preview.document?.name ?? ''}`"
    width="min(680px, 94vw)"
    append-to-body
  >
    <div class="terms-dialog__content">
      <RichTextContent :content="preview.document?.description ?? ''" />
    </div>
    <template #footer>
      <Button
        :label="t('features.activitySignup.terms.close')"
        text
        @click="preview.visible = false"
      />
      <Button
        :label="t('features.activitySignup.terms.reject')"
        type="danger"
        @click="rejectFromPreview"
      />
      <Button
        :label="t('features.activitySignup.terms.accept')"
        type="primary"
        @click="acceptFromPreview"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
.terms-dialog__lead {
  color: var(--ca-text);
  line-height: 1.55;
  margin-bottom: 14px;
}

.terms-dialog__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.terms-dialog__row {
  display: flex;
  align-items: center;
  gap: 10px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 12px 14px;
}

.terms-dialog__info {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.terms-dialog__link {
  background: none;
  border: none;
  padding: 0;
  font: inherit;
  font-weight: 600;
  color: var(--ca-orange);
  text-decoration: underline;
  cursor: pointer;
}

.terms-dialog__tag {
  font-size: 12px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 999px;
  white-space: nowrap;
}

.terms-dialog__tag--required {
  color: var(--ca-danger-ink);
  background: var(--ca-danger-soft);
}

.terms-dialog__tag--optional {
  color: var(--ca-text-muted);
  background: var(--ca-surface-2);
}

.terms-dialog__warning {
  margin-top: 14px;
  font-size: 13px;
  color: var(--ca-danger-ink);
}

.terms-dialog__content {
  max-height: 48vh;
  overflow-y: auto;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 14px 16px;
}
</style>
