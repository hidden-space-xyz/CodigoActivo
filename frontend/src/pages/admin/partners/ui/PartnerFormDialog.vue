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
import { ActionButton } from '@/shared/ui/action-button'

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
  <el-dialog
    v-model="visible"
    :title="
      partner
        ? $t('pages.admin.partners.form.editHeader')
        : $t('pages.admin.partners.form.newHeader')
    "
    width="min(460px, 92vw)"
  >
    <form class="form" @submit.prevent="save">
      <div class="ca-field">
        <label class="ca-field__label" for="partner-name">{{ $t('common.name') }}</label>
        <el-input
          id="partner-name"
          v-model="draft.name"
          :maxlength="PARTNER_NAME_MAX_LENGTH"
          :class="{ 'ca-invalid': errors.name }"
          :aria-invalid="!!errors.name"
          :aria-describedby="errors.name ? 'partner-name-error' : undefined"
        />
        <small v-if="errors.name" id="partner-name-error" class="ca-field__error">{{
          errors.name
        }}</small>
      </div>

      <div class="ca-field">
        <label class="ca-field__label" for="partner-from">{{
          $t('pages.admin.partners.form.fromDate')
        }}</label>
        <el-date-picker
          id="partner-from"
          v-model="draft.fromDate"
          type="date"
          :format="DATE_FORMAT"
          :disabled-date="(date: Date) => isDayAfter(date, new Date())"
          :class="{ 'ca-invalid': errors.fromDate }"
        />
        <small v-if="errors.fromDate" class="ca-field__error">{{ errors.fromDate }}</small>
      </div>

      <div class="ca-field">
        <label class="ca-field__label" for="partner-tier">{{
          $t('pages.admin.partners.form.tier')
        }}</label>
        <el-input-number
          id="partner-tier"
          v-model="draft.tier"
          :min="PARTNER_MIN_TIER"
          :max="PARTNER_MAX_TIER"
          :precision="0"
          controls-position="right"
        />
      </div>

      <div class="ca-field">
        <label class="ca-field__label" for="partner-website">{{
          $t('pages.admin.partners.form.website')
        }}</label>
        <el-input
          id="partner-website"
          v-model="draft.website"
          type="url"
          :maxlength="URL_MAX_LENGTH"
          :placeholder="$t('pages.admin.partners.form.urlPlaceholder')"
          :class="{ 'ca-invalid': errors.website }"
          :aria-invalid="!!errors.website"
          :aria-describedby="errors.website ? 'partner-website-error' : undefined"
        />
        <small v-if="errors.website" id="partner-website-error" class="ca-field__error">{{
          errors.website
        }}</small>
      </div>

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
    </form>

    <template #footer>
      <ActionButton
        :label="$t('common.cancel')"
        text
        :disabled="saving || uploading"
        @click="visible = false"
      />
      <ActionButton
        :label="$t('common.save')"
        type="primary"
        :loading="saving || uploading"
        @click="save"
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
</style>
