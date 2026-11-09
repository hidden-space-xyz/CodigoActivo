<script setup lang="ts">
import { watch } from 'vue'

import type { TermsDocument, TermsDocumentInput } from '@/entities/terms-document'
import { CATALOG_NAME_MAX_LENGTH } from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'
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
  <FormDialog
    v-model:visible="visible"
    data-testid="terms-document-form-dialog"
    :title="
      termsDocument
        ? $t('pages.admin.catalogs.termsDocuments.form.editHeader')
        : $t('pages.admin.catalogs.termsDocuments.form.newHeader')
    "
    width="min(860px, 94vw)"
    append-to-body
    :saving="saving"
    scroll-height="68vh"
    @submit="save"
  >
    <p v-if="termsDocument" class="ca-field__hint" data-testid="terms-document-form-version-hint">
      {{
        $t('pages.admin.catalogs.termsDocuments.form.versionHint', {
          version: termsDocument.version,
        })
      }}
    </p>
    <FormField
      v-slot="{ control }"
      for="terms-document-name"
      :label="$t('common.name')"
      :invalid="invalid('name')"
    >
      <el-input
        v-model="draft.name"
        v-bind="control"
        data-testid="terms-document-form-name"
        :maxlength="CATALOG_NAME_MAX_LENGTH"
      />
    </FormField>
    <FormField
      v-slot="{ invalid: refused }"
      :label="$t('pages.admin.catalogs.termsDocuments.form.content')"
      :invalid="invalid('description')"
      :error="errors.description"
      error-testid="terms-document-form-content-error"
    >
      <RichTextEditor
        v-model="draft.description"
        data-testid="terms-document-form-content"
        :label="$t('pages.admin.catalogs.termsDocuments.form.content')"
        :invalid="refused"
      />
    </FormField>
    <div class="ca-field">
      <div id="terms-document-kind-label" class="ca-field__label">
        {{ $t('pages.admin.catalogs.termsDocuments.form.kind') }}
      </div>
      <el-radio-group v-model="draft.kind" aria-labelledby="terms-document-kind-label">
        <el-radio value="Conditions" data-testid="terms-document-form-conditions">
          {{ $t('pages.admin.catalogs.termsDocuments.kinds.Conditions') }}
        </el-radio>
        <el-radio value="Consent" data-testid="terms-document-form-consent">
          {{ $t('pages.admin.catalogs.termsDocuments.kinds.Consent') }}
        </el-radio>
      </el-radio-group>
      <small class="ca-field__hint">
        {{
          draft.kind === 'Consent'
            ? $t('pages.admin.catalogs.termsDocuments.form.kindHints.Consent')
            : $t('pages.admin.catalogs.termsDocuments.form.kindHints.Conditions')
        }}
      </small>
    </div>
    <div class="ca-field">
      <el-switch
        id="terms-document-requires-signature"
        v-model="draft.requiresSignature"
        data-testid="terms-document-form-requires-signature"
        :active-text="$t('pages.admin.catalogs.termsDocuments.form.requiresSignature')"
      />
      <small class="ca-field__hint">
        {{ $t('pages.admin.catalogs.termsDocuments.form.requiresSignatureHint') }}
      </small>
    </div>
  </FormDialog>
</template>
