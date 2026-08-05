<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'

import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import type {
  ActivityResponse,
  ActivityRoleCapacityRequest,
  CreateActivityRequest,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import type {
  ActivityModalityTypeResponse,
  ActivityRoleTypeResponse,
} from '@/shared/api/generated/models'
import { toDateOnly } from '@/shared/lib'

const DATE_TIME_FORMAT = 'DD/MM/YYYY HH:mm'

const props = defineProps<{
  visible: boolean
  activity: ActivityResponse | null
  modalityTypes: ActivityModalityTypeResponse[]
  roleTypes: ActivityRoleTypeResponse[]
  saving: boolean
  eventStart?: string | null
  eventEnd?: string | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: CreateActivityRequest | UpdateActivityRequest]
}>()

interface ActivityForm {
  title: string
  description: string
  location: string
  modalityId: string
  activityStartsAt: Date | null
  activityEndsAt: Date | null
}

const form = reactive<ActivityForm>({
  title: '',
  description: '',
  location: '',
  modalityId: '',
  activityStartsAt: null,
  activityEndsAt: null,
})
const desiredCounts = ref<Record<string, number | undefined>>({})
const submitted = ref(false)
const {
  pickedFile,
  uploading,
  uploadError,
  missingThumbnail,
  reset: resetThumbnail,
  resolveThumbnailId,
} = useThumbnailUpload(() => props.activity?.thumbnailId)

const eventStartDay = computed(() => props.eventStart?.slice(0, 10) ?? '')
const eventEndDay = computed(() => props.eventEnd?.slice(0, 10) ?? '')

function outsideEventDay(date: Date): boolean {
  const day = toDateOnly(date)
  if (eventStartDay.value && day < eventStartDay.value) return true
  if (eventEndDay.value && day > eventEndDay.value) return true
  return false
}

function disabledEndDate(date: Date): boolean {
  if (outsideEventDay(date)) return true
  const start = form.activityStartsAt
  return !!start && toDateOnly(date) < toDateOnly(start)
}

const startMissing = computed(() => !form.activityStartsAt)
const endMissing = computed(() => !form.activityEndsAt)
const orderInvalid = computed(
  () =>
    !!form.activityStartsAt &&
    !!form.activityEndsAt &&
    form.activityEndsAt <= form.activityStartsAt,
)
const outsideEvent = computed(() => {
  const start = form.activityStartsAt
  const end = form.activityEndsAt
  if (!start || !end) return false
  if (eventStartDay.value && toDateOnly(start) < eventStartDay.value) return true
  if (eventEndDay.value && toDateOnly(end) > eventEndDay.value) return true
  return false
})
const datesValid = computed(
  () => !startMissing.value && !endMissing.value && !orderInvalid.value && !outsideEvent.value,
)
const locationMissing = computed(() => !form.location.trim())
const modalityMissing = computed(() => !form.modalityId)

function populate(): void {
  submitted.value = false
  resetThumbnail()
  form.title = props.activity?.title ?? ''
  form.description = props.activity?.description ?? ''
  form.location = props.activity?.location ?? ''
  form.modalityId = props.activity?.modalityId ?? ''
  form.activityStartsAt = props.activity?.activityStartsAt
    ? new Date(props.activity.activityStartsAt)
    : null
  form.activityEndsAt = props.activity?.activityEndsAt
    ? new Date(props.activity.activityEndsAt)
    : null
  populateDesiredCounts()
}

function populateDesiredCounts(): void {
  const saved = new Map(
    (props.activity?.roleCapacities ?? []).map((item) => [
      item.activityRoleTypeId ?? '',
      item.desiredCount ?? undefined,
    ]),
  )
  const next: Record<string, number | undefined> = {}
  for (const role of props.roleTypes) {
    if (role.id) next[role.id] = saved.get(role.id)
  }
  desiredCounts.value = next
}

watch([() => props.visible, () => props.activity], ([open]) => {
  if (!open) return
  populate()
})

watch(
  () => props.roleTypes,
  () => {
    if (props.visible) populateDesiredCounts()
  },
)

function close(): void {
  emit('update:visible', false)
}

async function save(): Promise<void> {
  submitted.value = true
  if (
    !form.title.trim() ||
    !form.description.trim() ||
    locationMissing.value ||
    modalityMissing.value ||
    missingThumbnail.value ||
    !datesValid.value
  ) {
    return
  }
  const { activityStartsAt, activityEndsAt } = form
  if (!activityStartsAt || !activityEndsAt) return
  const thumbnailId = await resolveThumbnailId()
  if (!thumbnailId) return
  const roleCapacities: ActivityRoleCapacityRequest[] = []
  for (const [activityRoleTypeId, desiredCount] of Object.entries(desiredCounts.value)) {
    if (desiredCount != null && desiredCount >= 1) {
      roleCapacities.push({ activityRoleTypeId, desiredCount })
    }
  }
  emit('submit', {
    title: form.title.trim(),
    description: form.description.trim(),
    location: form.location.trim(),
    activityModalityTypeId: form.modalityId,
    activityStartsAt: activityStartsAt.toISOString(),
    activityEndsAt: activityEndsAt.toISOString(),
    thumbnailId,
    roleCapacities: roleCapacities.length > 0 ? roleCapacities : null,
  } satisfies CreateActivityRequest)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="
      activity
        ? $t('features.manageActivities.editHeader')
        : $t('features.manageActivities.newHeader')
    "
    width="min(560px, 92vw)"
    @update:model-value="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label>{{ $t('features.manageActivities.fields.title') }}</label>
        <el-input
          v-model="form.title"
          :maxlength="200"
          :class="{ 'ca-invalid': submitted && !form.title.trim() }"
        />
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageActivities.fields.description') }}</label>
        <el-input
          v-model="form.description"
          type="textarea"
          :maxlength="4000"
          :autosize="{ minRows: 3 }"
          :class="{ 'ca-invalid': submitted && !form.description.trim() }"
        />
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>{{ $t('features.manageActivities.fields.modality') }}</label>
          <el-select
            v-model="form.modalityId"
            :placeholder="$t('features.manageActivities.modalityPlaceholder')"
            :class="{ 'ca-invalid': submitted && modalityMissing }"
          >
            <el-option
              v-for="modality in modalityTypes"
              :key="modality.id ?? ''"
              :label="modality.name ?? ''"
              :value="modality.id ?? ''"
            />
          </el-select>
          <small v-if="submitted && modalityMissing" class="form__error">{{
            $t('features.manageActivities.errors.modalityRequired')
          }}</small>
        </div>
        <div class="form__field">
          <label>{{ $t('features.manageActivities.fields.location') }}</label>
          <el-input
            v-model="form.location"
            :maxlength="200"
            :class="{ 'ca-invalid': submitted && locationMissing }"
          />
          <small v-if="submitted && locationMissing" class="form__error">{{
            $t('features.manageActivities.errors.locationRequired')
          }}</small>
        </div>
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>{{ $t('features.manageActivities.fields.start') }}</label>
          <el-date-picker
            v-model="form.activityStartsAt"
            type="datetime"
            :format="DATE_TIME_FORMAT"
            :disabled-date="outsideEventDay"
            :class="{ 'ca-invalid': submitted && (startMissing || outsideEvent) }"
          />
          <small v-if="submitted && startMissing" class="form__error">{{
            $t('features.manageActivities.errors.startRequired')
          }}</small>
        </div>
        <div class="form__field">
          <label>{{ $t('features.manageActivities.fields.end') }}</label>
          <el-date-picker
            v-model="form.activityEndsAt"
            type="datetime"
            :format="DATE_TIME_FORMAT"
            :disabled-date="disabledEndDate"
            :class="{
              'ca-invalid': submitted && (endMissing || orderInvalid || outsideEvent),
            }"
          />
          <small v-if="submitted && endMissing" class="form__error">{{
            $t('features.manageActivities.errors.endRequired')
          }}</small>
          <small v-else-if="submitted && orderInvalid" class="form__error">{{
            $t('features.manageActivities.errors.orderInvalid')
          }}</small>
        </div>
      </div>
      <small v-if="submitted && outsideEvent" class="form__error">{{
        $t('features.manageActivities.errors.outsideEvent')
      }}</small>
      <div v-if="roleTypes.length" class="form__field">
        <label>{{ $t('features.manageActivities.fields.desiredCounts') }}</label>
        <div class="form__capacities">
          <div v-for="role in roleTypes" :key="role.id ?? ''" class="form__capacity">
            <span class="form__capacity-name">{{ role.name }}</span>
            <el-input-number
              v-model="desiredCounts[role.id ?? '']"
              :min="1"
              :max="10000"
              controls-position="right"
              :placeholder="$t('features.manageActivities.noTargetPlaceholder')"
            />
          </div>
        </div>
        <small class="form__hint">
          {{ $t('features.manageActivities.hint') }}
        </small>
      </div>
      <div class="form__field">
        <label>{{ $t('common.image') }}</label>
        <ThumbnailField
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
      <Button :label="$t('common.cancel')" text :disabled="saving || uploading" @click="close" />
      <Button
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

.form__field label {
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

.form :deep(.ca-invalid) {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}

.form :deep(.ca-invalid .el-select__wrapper) {
  box-shadow: 0 0 0 1px var(--ca-danger) inset;
}
</style>
