<script setup lang="ts">
import { computed, watch } from 'vue'

import type {
  ActivityDetail,
  ActivityInput,
  ActivityModality,
  ActivityRole,
} from '@/entities/activity'
import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import { isDayBefore } from '@/shared/lib/date'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'

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
      activity
        ? $t('pages.admin.eventDetail.activities.form.editHeader')
        : $t('pages.admin.eventDetail.activities.form.newHeader')
    "
    width="min(560px, 92vw)"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label for="activity-title">{{
          $t('pages.admin.eventDetail.activities.form.fields.title')
        }}</label>
        <el-input
          id="activity-title"
          v-model="draft.title"
          :maxlength="200"
          :class="{ 'ca-invalid': invalid('title') }"
        />
      </div>
      <div class="form__field">
        <label for="activity-description">{{
          $t('pages.admin.eventDetail.activities.form.fields.description')
        }}</label>
        <el-input
          id="activity-description"
          v-model="draft.description"
          type="textarea"
          :maxlength="4000"
          :autosize="{ minRows: 3 }"
          :class="{ 'ca-invalid': invalid('description') }"
        />
      </div>
      <div class="form__row">
        <div class="form__field">
          <label for="activity-modality">{{
            $t('pages.admin.eventDetail.activities.form.fields.modality')
          }}</label>
          <el-select
            id="activity-modality"
            v-model="draft.modalityId"
            :placeholder="$t('pages.admin.eventDetail.activities.form.modalityPlaceholder')"
            :class="{ 'ca-invalid': invalid('modalityId') }"
          >
            <el-option
              v-for="modality in modalities"
              :key="modality.id"
              :label="modality.name"
              :value="modality.id"
            />
          </el-select>
          <small v-if="errors.modalityId" class="form__error">{{ errors.modalityId }}</small>
        </div>
        <div class="form__field">
          <label for="activity-location">{{
            $t('pages.admin.eventDetail.activities.form.fields.location')
          }}</label>
          <el-input
            id="activity-location"
            v-model="draft.location"
            :maxlength="200"
            :class="{ 'ca-invalid': invalid('location') }"
          />
          <small v-if="errors.location" class="form__error">{{ errors.location }}</small>
        </div>
      </div>
      <div class="form__row">
        <div class="form__field">
          <label for="activity-start">{{
            $t('pages.admin.eventDetail.activities.form.fields.start')
          }}</label>
          <el-date-picker
            id="activity-start"
            v-model="draft.startsAt"
            type="datetime"
            :format="DATE_TIME_FORMAT"
            :disabled-date="outsideEvent"
            :class="{ 'ca-invalid': invalid('startsAt') }"
          />
          <small v-if="errors.startsAt" class="form__error">{{ errors.startsAt }}</small>
        </div>
        <div class="form__field">
          <label for="activity-end">{{
            $t('pages.admin.eventDetail.activities.form.fields.end')
          }}</label>
          <el-date-picker
            id="activity-end"
            v-model="draft.endsAt"
            type="datetime"
            :format="DATE_TIME_FORMAT"
            :disabled-date="beforeStartOrOutsideEvent"
            :class="{ 'ca-invalid': invalid('endsAt') }"
          />
          <small v-if="errors.endsAt" class="form__error">{{ errors.endsAt }}</small>
        </div>
      </div>
      <small v-if="errors.schedule" class="form__error">{{ errors.schedule }}</small>
      <div v-if="roles.length" class="form__field">
        <div id="activity-capacities-label" class="form__label">
          {{ $t('pages.admin.eventDetail.activities.form.fields.desiredCounts') }}
        </div>
        <div class="form__capacities" role="group" aria-labelledby="activity-capacities-label">
          <div v-for="role in roles" :key="role.id" class="form__capacity">
            <span class="form__capacity-name">{{ role.name }}</span>
            <el-input-number
              v-model="draft.desiredCounts[role.id]"
              :aria-label="role.name"
              :min="1"
              :max="10000"
              controls-position="right"
              :placeholder="$t('pages.admin.eventDetail.activities.form.noTargetPlaceholder')"
            />
          </div>
        </div>
        <small class="form__hint">
          {{ $t('pages.admin.eventDetail.activities.form.hint') }}
        </small>
      </div>
      <div class="form__field">
        <div id="activity-image-label" class="form__label">{{ $t('common.image') }}</div>
        <ThumbnailField
          role="group"
          aria-labelledby="activity-image-label"
          :existing-thumbnail-id="activity?.thumbnailId"
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

.form__row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
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

.form__hint {
  color: var(--ca-text-muted);
  font-size: 12px;
}

.form :deep(.el-select),
.form :deep(.el-date-editor),
.form :deep(.el-input-number) {
  width: 100%;
}

@media (max-width: 640px) {
  .form__row,
  .form__capacities {
    grid-template-columns: 1fr;
  }
}
</style>
