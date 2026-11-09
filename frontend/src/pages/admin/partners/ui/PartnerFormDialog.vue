<script setup lang="ts">
import { watch } from 'vue'

import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import type { Partner, PartnerInput } from '@/entities/partner'
import {
  PARTNER_MAX_TIER,
  PARTNER_MIN_TIER,
  PARTNER_NAME_MAX_LENGTH,
  URL_MAX_LENGTH,
} from '@/shared/config'
import { isDayAfter } from '@/shared/lib/date'
import { useForm } from '@/shared/lib/form'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'

import { readPartnerDraft, toPartnerDraft } from '../model/partner-form'

const DATE_FORMAT = 'DD/MM/YYYY'

/** Opens the dialog; each time it opens the form is refilled from `partner`. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Partner being edited; `null` opens the dialog in create mode. */
  partner: Partner | null
  /** Shows a loading state on the save button while the parent persists the partner. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid and the logo is uploaded. */
  submit: [input: PartnerInput]
}>()

const form = useForm({ initial: () => toPartnerDraft(null), read: readPartnerDraft })
const { draft, errors, submitted } = form
const thumbnail = useThumbnailUpload(() => props.partner?.thumbnailId)
const { pickedFile, uploading, uploadError, missingThumbnail } = thumbnail

watch(visible, (open) => {
  if (!open) return
  form.reset(toPartnerDraft(props.partner))
  thumbnail.reset()
})

async function save(): Promise<void> {
  const value = form.submit()
  if (!value) return
  const thumbnailId = await thumbnail.resolveThumbnailId()
  if (!thumbnailId) return
  emit('submit', { ...value, thumbnailId })
}
</script>

<template>
  <FormDialog
    v-model:visible="visible"
    data-testid="partner-form-dialog"
    :title="
      partner
        ? $t('pages.admin.partners.form.editHeader')
        : $t('pages.admin.partners.form.newHeader')
    "
    width="min(460px, 92vw)"
    :saving="saving || uploading"
    @submit="save"
  >
    <FormField
      v-slot="{ control }"
      for="partner-name"
      :label="$t('common.name')"
      :error="errors.name"
      error-testid="partner-form-name-error"
    >
      <el-input
        v-model="draft.name"
        v-bind="control"
        data-testid="partner-form-name"
        :maxlength="PARTNER_NAME_MAX_LENGTH"
      />
    </FormField>

    <FormField
      v-slot="{ control }"
      for="partner-from"
      :label="$t('pages.admin.partners.form.fromDate')"
      :error="errors.fromDate"
      error-testid="partner-form-from-date-error"
      data-testid="partner-form-from-date"
    >
      <el-date-picker
        :id="control.id"
        v-model="draft.fromDate"
        type="date"
        :format="DATE_FORMAT"
        :disabled-date="(date: Date) => isDayAfter(date, new Date())"
        :class="control.class"
      />
    </FormField>

    <FormField for="partner-tier" :label="$t('pages.admin.partners.form.tier')">
      <el-input-number
        id="partner-tier"
        v-model="draft.tier"
        data-testid="partner-form-tier"
        :min="PARTNER_MIN_TIER"
        :max="PARTNER_MAX_TIER"
        :precision="0"
        controls-position="right"
      />
    </FormField>

    <FormField
      v-slot="{ control }"
      for="partner-website"
      :label="$t('pages.admin.partners.form.website')"
      :error="errors.website"
      error-testid="partner-form-website-error"
    >
      <el-input
        v-model="draft.website"
        v-bind="control"
        data-testid="partner-form-website"
        type="url"
        :maxlength="URL_MAX_LENGTH"
        :placeholder="$t('pages.admin.partners.form.urlPlaceholder')"
      />
    </FormField>

    <div class="ca-field">
      <div id="partner-image-label" class="ca-field__label">{{ $t('common.image') }}</div>
      <ThumbnailField
        role="group"
        aria-labelledby="partner-image-label"
        :existing-thumbnail-id="partner?.thumbnailId"
        :invalid="submitted && missingThumbnail"
        :upload-error="uploadError"
        @update:file="pickedFile = $event"
      />
    </div>
  </FormDialog>
</template>
