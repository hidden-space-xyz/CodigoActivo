<script setup lang="ts">
import { watch } from 'vue'

import type { TermsDocument, TermsDocumentInput } from '@/entities/terms-document'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'

import { readTermsDocumentDraft, toTermsDocumentDraft } from '../model/terms-document-form'

/** Opens the dialog; each time it opens the form is refilled from `termsDocument`. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Terms document being edited; `null` opens the dialog in create mode. */
  termsDocument: TermsDocument | null
  /** Shows a loading state on the save button while the parent persists the terms document. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid. */
  submit: [input: TermsDocumentInput]
}>()

const form = useForm({ initial: () => toTermsDocumentDraft(null), read: readTermsDocumentDraft })
const { draft, errors, invalid } = form

watch(visible, (open) => {
  if (open) form.reset(toTermsDocumentDraft(props.termsDocument))
})

function save(): void {
  const value = form.submit()
  if (value) emit('submit', value)
}
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="
      termsDocument
        ? $t('pages.admin.catalogs.termsDocuments.form.editHeader')
        : $t('pages.admin.catalogs.termsDocuments.form.newHeader')
    "
    width="min(860px, 94vw)"
    append-to-body
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label for="terms-document-name">{{ $t('common.name') }}</label>
        <el-input
          id="terms-document-name"
          v-model="draft.name"
          :maxlength="120"
          :class="{ 'ca-invalid': invalid('name') }"
        />
      </div>
      <div class="form__field">
        <div class="form__label">{{ $t('pages.admin.catalogs.termsDocuments.form.content') }}</div>
        <RichTextEditor
          v-model="draft.description"
          :label="$t('pages.admin.catalogs.termsDocuments.form.content')"
          :invalid="invalid('description')"
        />
        <small v-if="errors.description" class="form__error">{{ errors.description }}</small>
      </div>
      <div class="form__field">
        <div id="terms-document-kind-label" class="form__label">
          {{ $t('pages.admin.catalogs.termsDocuments.form.kind') }}
        </div>
        <el-radio-group v-model="draft.kind" aria-labelledby="terms-document-kind-label">
          <el-radio value="Conditions">
            {{ $t('pages.admin.catalogs.termsDocuments.kinds.Conditions') }}
          </el-radio>
          <el-radio value="Consent">
            {{ $t('pages.admin.catalogs.termsDocuments.kinds.Consent') }}
          </el-radio>
        </el-radio-group>
        <small class="form__hint">
          {{
            draft.kind === 'Consent'
              ? $t('pages.admin.catalogs.termsDocuments.form.kindHints.Consent')
              : $t('pages.admin.catalogs.termsDocuments.form.kindHints.Conditions')
          }}
        </small>
      </div>
      <div class="form__field">
        <el-switch
          id="terms-document-requires-signature"
          v-model="draft.requiresSignature"
          :active-text="$t('pages.admin.catalogs.termsDocuments.form.requiresSignature')"
        />
        <small class="form__hint">
          {{ $t('pages.admin.catalogs.termsDocuments.form.requiresSignatureHint') }}
        </small>
      </div>
    </form>
    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="visible = false" />
      <ActionButton :label="$t('common.save')" type="primary" :loading="saving" @click="save" />
    </template>
  </el-dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding-top: 6px;
  max-height: 68vh;
  overflow-y: auto;
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
  color: var(--ca-text-muted);
  font-size: 12.5px;
}

@media (max-width: 640px) {
  .form {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
