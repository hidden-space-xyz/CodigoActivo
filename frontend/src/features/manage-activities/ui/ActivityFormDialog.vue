<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'

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
import { parseDateOnly, toDateOnly } from '@/shared/lib'

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
const desiredCounts = ref<Record<string, number | null>>({})
const submitted = ref(false)
const {
  pickedFile,
  uploading,
  uploadError,
  missingThumbnail,
  reset: resetThumbnail,
  resolveThumbnailId,
} = useThumbnailUpload(() => props.activity?.thumbnailId)

const eventStartDate = computed(() => parseDateOnly(props.eventStart))
const eventEndDate = computed(() => parseDateOnly(props.eventEnd))
const minDate = computed(() => eventStartDate.value ?? undefined)
const maxDate = computed(() => {
  const end = eventEndDate.value
  return end
    ? new Date(end.getFullYear(), end.getMonth(), end.getDate(), 23, 59, 59, 999)
    : undefined
})

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
  const eventStart = props.eventStart?.slice(0, 10)
  const eventEnd = props.eventEnd?.slice(0, 10)
  // Compare the activity's local calendar day (matching the picker's local min/max bounds and the
  // backend's timezone-aware check), not its UTC day which drifts across midnight.
  if (eventStart && toDateOnly(start) < eventStart) return true
  if (eventEnd && toDateOnly(end) > eventEnd) return true
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
      item.desiredCount ?? null,
    ]),
  )
  const next: Record<string, number | null> = {}
  for (const role of props.roleTypes) {
    if (role.id) next[role.id] = saved.get(role.id) ?? null
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
  <Dialog
    :visible="visible"
    modal
    :header="
      activity ? $t('features.manageActivities.editHeader') : $t('features.manageActivities.newHeader')
    "
    :style="{ width: '560px' }"
    @update:visible="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label>{{ $t('features.manageActivities.fields.title') }}</label>
        <InputText
          v-model="form.title"
          :maxlength="200"
          :invalid="submitted && !form.title.trim()"
          fluid
        />
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageActivities.fields.description') }}</label>
        <Textarea
          v-model="form.description"
          :maxlength="4000"
          :invalid="submitted && !form.description.trim()"
          rows="3"
          auto-resize
          fluid
        />
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>{{ $t('features.manageActivities.fields.modality') }}</label>
          <Select
            v-model="form.modalityId"
            :options="modalityTypes"
            option-label="name"
            option-value="id"
            :placeholder="$t('features.manageActivities.modalityPlaceholder')"
            :invalid="submitted && modalityMissing"
            fluid
          />
          <small v-if="submitted && modalityMissing" class="form__error"
            >{{ $t('features.manageActivities.errors.modalityRequired') }}</small
          >
        </div>
        <div class="form__field">
          <label>{{ $t('features.manageActivities.fields.location') }}</label>
          <InputText
            v-model="form.location"
            :maxlength="200"
            :invalid="submitted && locationMissing"
            fluid
          />
          <small v-if="submitted && locationMissing" class="form__error"
            >{{ $t('features.manageActivities.errors.locationRequired') }}</small
          >
        </div>
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>{{ $t('features.manageActivities.fields.start') }}</label>
          <DatePicker
            v-model="form.activityStartsAt"
            show-time
            hour-format="24"
            date-format="dd/mm/yy"
            :min-date="minDate"
            :max-date="maxDate"
            :invalid="submitted && (startMissing || outsideEvent)"
            fluid
          />
          <small v-if="submitted && startMissing" class="form__error"
            >{{ $t('features.manageActivities.errors.startRequired') }}</small
          >
        </div>
        <div class="form__field">
          <label>{{ $t('features.manageActivities.fields.end') }}</label>
          <DatePicker
            v-model="form.activityEndsAt"
            show-time
            hour-format="24"
            date-format="dd/mm/yy"
            :min-date="form.activityStartsAt ?? minDate"
            :max-date="maxDate"
            :invalid="submitted && (endMissing || orderInvalid || outsideEvent)"
            fluid
          />
          <small v-if="submitted && endMissing" class="form__error"
            >{{ $t('features.manageActivities.errors.endRequired') }}</small
          >
          <small v-else-if="submitted && orderInvalid" class="form__error"
            >{{ $t('features.manageActivities.errors.orderInvalid') }}</small
          >
        </div>
      </div>
      <small v-if="submitted && outsideEvent" class="form__error"
        >{{ $t('features.manageActivities.errors.outsideEvent') }}</small
      >
      <div v-if="roleTypes.length" class="form__field">
        <label>{{ $t('features.manageActivities.fields.desiredCounts') }}</label>
        <div class="form__capacities">
          <div v-for="role in roleTypes" :key="role.id ?? ''" class="form__capacity">
            <span class="form__capacity-name">{{ role.name }}</span>
            <InputNumber
              v-model="desiredCounts[role.id ?? '']"
              :min="1"
              :max="10000"
              :placeholder="$t('features.manageActivities.noTargetPlaceholder')"
              fluid
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
        <small v-if="submitted && missingThumbnail" class="form__error"
          >{{ $t('common.imageRequired') }}</small
        >
        <small v-if="uploadError" class="form__error">{{ uploadError }}</small>
      </div>
    </form>

    <template #footer>
      <Button
        :label="$t('common.cancel')"
        text
        severity="secondary"
        :disabled="saving || uploading"
        @click="close"
      />
      <Button :label="$t('common.save')" :loading="saving || uploading" @click="save" />
    </template>
  </Dialog>
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
</style>
