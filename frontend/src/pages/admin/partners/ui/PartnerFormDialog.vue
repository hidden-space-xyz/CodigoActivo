<script setup lang="ts">
import { watch } from 'vue'

import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import type { Partner, PartnerInput } from '@/entities/partner'
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
  if (!value || missingThumbnail.value) return
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
      <div class="form__field">
        <label for="partner-name">{{ $t('common.name') }}</label>
        <el-input
          id="partner-name"
          v-model="draft.name"
          :maxlength="200"
          :class="{ 'ca-invalid': errors.name }"
        />
        <small v-if="errors.name" class="form__error">{{ errors.name }}</small>
      </div>

      <div class="form__field">
        <label for="partner-from">{{ $t('pages.admin.partners.form.fromDate') }}</label>
        <el-date-picker
          id="partner-from"
          v-model="draft.fromDate"
          type="date"
          :format="DATE_FORMAT"
          :disabled-date="(date: Date) => isDayAfter(date, new Date())"
          :class="{ 'ca-invalid': errors.fromDate }"
        />
        <small v-if="errors.fromDate" class="form__error">{{ errors.fromDate }}</small>
      </div>

      <div class="form__field">
        <label for="partner-tier">{{ $t('pages.admin.partners.form.tier') }}</label>
        <el-input-number
          id="partner-tier"
          v-model="draft.tier"
          :min="0"
          controls-position="right"
        />
      </div>

      <div class="form__field">
        <label for="partner-website">{{ $t('pages.admin.partners.form.website') }}</label>
        <el-input
          id="partner-website"
          v-model="draft.website"
          :placeholder="$t('pages.admin.partners.form.urlPlaceholder')"
        />
      </div>

      <div class="form__field">
        <div id="partner-image-label" class="form__label">{{ $t('common.image') }}</div>
        <ThumbnailField
          role="group"
          aria-labelledby="partner-image-label"
          :existing-thumbnail-id="partner?.thumbnailId"
          :invalid="submitted && missingThumbnail"
          @update:file="pickedFile = $event"
        />
        <small v-if="submitted && missingThumbnail" class="form__error">{{
          $t('common.imageRequired')
        }}</small>
        <small v-if="uploadError" class="form__error">{{ uploadError }}</small>
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

.form :deep(.el-date-editor),
.form :deep(.el-input-number) {
  width: 100%;
}
</style>
