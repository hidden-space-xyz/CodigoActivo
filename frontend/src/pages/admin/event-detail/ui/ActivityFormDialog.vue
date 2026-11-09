<script setup lang="ts">
import { computed, watch } from 'vue'

import type {
  ActivityDetail,
  ActivityInput,
  ActivityModality,
  ActivityRole,
} from '@/entities/activity'
import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import {
  ACTIVITY_DESCRIPTION_MAX_LENGTH,
  ACTIVITY_LOCATION_MAX_LENGTH,
  ROLE_MAX_DESIRED_COUNT,
  ROLE_MIN_DESIRED_COUNT,
  TITLE_MAX_LENGTH,
} from '@/shared/config'
import { isDayBefore } from '@/shared/lib/date'
import { useForm } from '@/shared/lib/form'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'

import {
  type EventDays,
  isOutsideEvent,
  readActivityDraft,
  toActivityDraft,
  toDesiredCounts,
} from '../model/activity-form'

const DATE_TIME_FORMAT = 'DD/MM/YYYY HH:mm'

/** Opens the dialog; opening it, or changing `activity` while open, refills the form. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Activity being edited; `null` opens the dialog in create mode. */
  activity: ActivityDetail | null
  /** Options of the required modality select. */
  modalities: readonly ActivityModality[]
  /** Roles offered a desired-count input. */
  roles: readonly ActivityRole[]
  /** Shows a loading state on the save button while the parent persists the activity. */
  saving: boolean
  /** Event start as an ISO day or instant; days before it cannot be picked. */
  eventStart?: string | null
  /** Event end as an ISO day or instant; days after it cannot be picked. */
  eventEnd?: string | null
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid and the thumbnail is uploaded. */
  submit: [input: ActivityInput]
}>()

const eventDays = computed<EventDays>(() => ({
  startsAt: props.eventStart ?? null,
  endsAt: props.eventEnd ?? null,
}))
const form = useForm({
  initial: () => toActivityDraft(null, []),
  read: (draft) => readActivityDraft(draft, eventDays.value),
})
const { draft, submitted, errors, invalid } = form
const thumbnail = useThumbnailUpload(() => props.activity?.thumbnailId)
const { pickedFile, uploading, uploadError, missingThumbnail } = thumbnail

watch([visible, () => props.activity], ([open]) => {
  if (!open) return
  form.reset(toActivityDraft(props.activity, props.roles))
  thumbnail.reset()
})

watch(
  () => props.roles,
  (roles) => {
    if (visible.value) draft.desiredCounts = toDesiredCounts(props.activity, roles)
  },
)

function outsideEvent(date: Date): boolean {
  return isOutsideEvent(date, eventDays.value)
}

function beforeStartOrOutsideEvent(date: Date): boolean {
  return outsideEvent(date) || isDayBefore(date, draft.startsAt)
}

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
    :title="
      activity
        ? $t('pages.admin.eventDetail.activities.form.editHeader')
        : $t('pages.admin.eventDetail.activities.form.newHeader')
    "
    width="min(560px, 92vw)"
    data-testid="activity-form-dialog"
    :saving="saving || uploading"
    @submit="save"
  >
    <FormField
      v-slot="{ control }"
      for="activity-title"
      :label="$t('common.title')"
      :invalid="invalid('title')"
    >
      <el-input
        v-model="draft.title"
        v-bind="control"
        :maxlength="TITLE_MAX_LENGTH"
        data-testid="activity-form-title"
      />
    </FormField>
    <FormField
      v-slot="{ control }"
      for="activity-description"
      :label="$t('common.description')"
      :invalid="invalid('description')"
    >
      <el-input
        v-model="draft.description"
        v-bind="control"
        type="textarea"
        :maxlength="ACTIVITY_DESCRIPTION_MAX_LENGTH"
        :autosize="{ minRows: 3 }"
        data-testid="activity-form-description"
      />
    </FormField>
    <div class="ca-form-grid">
      <FormField
        v-slot="{ control }"
        for="activity-modality"
        :label="$t('pages.admin.eventDetail.activities.form.fields.modality')"
        :invalid="invalid('modalityId')"
        :error="errors.modalityId"
        error-testid="activity-form-modality-error"
      >
        <el-select
          :id="control.id"
          v-model="draft.modalityId"
          :placeholder="$t('pages.admin.eventDetail.activities.form.modalityPlaceholder')"
          :class="control.class"
          data-testid="activity-form-modality"
        >
          <el-option
            v-for="modality in modalities"
            :key="modality.id"
            :label="modality.name"
            :value="modality.id"
            :data-testid="`option-${modality.id}`"
          />
        </el-select>
      </FormField>
      <FormField
        v-slot="{ control }"
        for="activity-location"
        :label="$t('pages.admin.eventDetail.activities.form.fields.location')"
        :invalid="invalid('location')"
        :error="errors.location"
        error-testid="activity-form-location-error"
      >
        <el-input
          v-model="draft.location"
          v-bind="control"
          :maxlength="ACTIVITY_LOCATION_MAX_LENGTH"
          data-testid="activity-form-location"
        />
      </FormField>
    </div>
    <div class="ca-form-grid">
      <FormField
        v-slot="{ control }"
        for="activity-start"
        :label="$t('pages.admin.eventDetail.activities.form.fields.start')"
        :invalid="invalid('startsAt')"
        :error="errors.startsAt"
        error-testid="activity-form-start-error"
        data-testid="activity-form-start"
      >
        <el-date-picker
          :id="control.id"
          v-model="draft.startsAt"
          type="datetime"
          :format="DATE_TIME_FORMAT"
          :disabled-date="outsideEvent"
          :class="control.class"
        />
      </FormField>
      <FormField
        v-slot="{ control }"
        for="activity-end"
        :label="$t('pages.admin.eventDetail.activities.form.fields.end')"
        :invalid="invalid('endsAt')"
        :error="errors.endsAt"
        error-testid="activity-form-end-error"
        data-testid="activity-form-end"
      >
        <el-date-picker
          :id="control.id"
          v-model="draft.endsAt"
          type="datetime"
          :format="DATE_TIME_FORMAT"
          :disabled-date="beforeStartOrOutsideEvent"
          :class="control.class"
        />
      </FormField>
    </div>
    <small v-if="errors.schedule" class="ca-field__error">{{ errors.schedule }}</small>
    <div v-if="roles.length" class="ca-field">
      <div id="activity-capacities-label" class="ca-field__label">
        {{ $t('pages.admin.eventDetail.activities.form.fields.desiredCounts') }}
      </div>
      <div class="form__capacities" role="group" aria-labelledby="activity-capacities-label">
        <div
          v-for="role in roles"
          :key="role.id"
          class="form__capacity"
          data-testid="activity-form-capacity"
        >
          <span class="form__capacity-name">{{ role.name }}</span>
          <el-input-number
            v-model="draft.desiredCounts[role.id]"
            :aria-label="role.name"
            :min="ROLE_MIN_DESIRED_COUNT"
            :max="ROLE_MAX_DESIRED_COUNT"
            :precision="0"
            controls-position="right"
            :placeholder="$t('pages.admin.eventDetail.activities.form.noTargetPlaceholder')"
          />
        </div>
      </div>
      <small class="ca-field__hint">
        {{ $t('pages.admin.eventDetail.activities.form.hint') }}
      </small>
    </div>
    <div class="ca-field">
      <div id="activity-image-label" class="ca-field__label">{{ $t('common.image') }}</div>
      <ThumbnailField
        role="group"
        aria-labelledby="activity-image-label"
        :existing-thumbnail-id="activity?.thumbnailId"
        :invalid="submitted && missingThumbnail"
        :upload-error="uploadError"
        @update:file="pickedFile = $event"
      />
    </div>
  </FormDialog>
</template>

<style scoped>
.form__capacities {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 10px;
}

.form__capacity {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.form__capacity-name {
  font-size: 12.5px;
  color: var(--ca-text);
}

@media (max-width: 640px) {
  .form__capacities {
    grid-template-columns: 1fr;
  }
}
</style>
