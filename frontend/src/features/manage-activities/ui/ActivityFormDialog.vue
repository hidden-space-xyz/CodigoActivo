<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'
import Textarea from 'primevue/textarea'

import { ThumbnailField, uploadThumbnail } from '@/entities/file'
import type {
  ActivityResponse,
  ActivityRoleTypeResponse,
  CreateActivityRequest,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/lib'

const props = defineProps<{
  visible: boolean
  activity: ActivityResponse | null
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
  activityStartsAt: Date | null
  activityEndsAt: Date | null
  roleIds: string[]
}

const form = reactive<ActivityForm>({
  title: '',
  description: '',
  activityStartsAt: null,
  activityEndsAt: null,
  roleIds: [],
})
const submitted = ref(false)
const pickedFile = ref<File | null>(null)
const uploading = ref(false)
const uploadError = ref('')

const missingThumbnail = computed(() => !pickedFile.value && !props.activity?.thumbnailId)

function parseDateOnly(value?: string | null): Date | null {
  if (!value) return null
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return null
  return new Date(year, month - 1, day)
}

function utcDay(date: Date): string {
  return date.toISOString().slice(0, 10)
}

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
  if (eventStart && utcDay(start) < eventStart) return true
  if (eventEnd && utcDay(end) > eventEnd) return true
  return false
})
const datesValid = computed(
  () => !startMissing.value && !endMissing.value && !orderInvalid.value && !outsideEvent.value,
)

function populate(): void {
  submitted.value = false
  pickedFile.value = null
  uploadError.value = ''
  form.title = props.activity?.title ?? ''
  form.description = props.activity?.description ?? ''
  form.activityStartsAt = props.activity?.activityStartsAt
    ? new Date(props.activity.activityStartsAt)
    : null
  form.activityEndsAt = props.activity?.activityEndsAt
    ? new Date(props.activity.activityEndsAt)
    : null
  form.roleIds = []
  for (const role of props.activity?.allowedRoleTypes ?? []) {
    if (!role.roleTypeId) continue
    form.roleIds.push(role.roleTypeId)
  }
}

watch([() => props.visible, () => props.activity], ([open]) => {
  if (!open) return
  populate()
})

function close(): void {
  emit('update:visible', false)
}

async function save(): Promise<void> {
  submitted.value = true
  uploadError.value = ''
  if (
    !form.title.trim() ||
    !form.description.trim() ||
    missingThumbnail.value ||
    !datesValid.value
  ) {
    return
  }
  const { activityStartsAt, activityEndsAt } = form
  if (!activityStartsAt || !activityEndsAt) return
  const body: CreateActivityRequest = {
    title: form.title.trim(),
    description: form.description.trim(),
    activityStartsAt: activityStartsAt.toISOString(),
    activityEndsAt: activityEndsAt.toISOString(),
    allowedRoleTypes: form.roleIds.map((id) => ({ activityRoleTypeId: id })),
  }
  uploading.value = true
  try {
    if (pickedFile.value) {
      body.thumbnailId = await uploadThumbnail(pickedFile.value, props.activity?.thumbnailId)
    } else if (props.activity?.thumbnailId) {
      body.thumbnailId = props.activity.thumbnailId
    }
    emit('submit', body)
  } catch (error) {
    uploadError.value = getErrorMessage(error, 'No se pudo subir la imagen.')
  } finally {
    uploading.value = false
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="activity ? 'Editar actividad' : 'Nueva actividad'"
    :style="{ width: '560px' }"
    @update:visible="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label>Título</label>
        <InputText v-model="form.title" :invalid="submitted && !form.title.trim()" fluid />
      </div>
      <div class="form__field">
        <label>Descripción</label>
        <Textarea
          v-model="form.description"
          :invalid="submitted && !form.description.trim()"
          rows="3"
          auto-resize
          fluid
        />
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>Inicio</label>
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
            >La fecha y hora de inicio son obligatorias.</small
          >
        </div>
        <div class="form__field">
          <label>Fin</label>
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
            >La fecha y hora de fin son obligatorias.</small
          >
          <small v-else-if="submitted && orderInvalid" class="form__error"
            >El fin debe ser posterior al inicio.</small
          >
        </div>
      </div>
      <small v-if="submitted && outsideEvent" class="form__error"
        >La actividad debe estar dentro de las fechas del evento.</small
      >
      <div class="form__field">
        <label>Roles permitidos</label>
        <MultiSelect
          v-model="form.roleIds"
          :options="roleTypes"
          option-label="name"
          option-value="id"
          placeholder="Selecciona roles"
          fluid
        />
      </div>
      <div class="form__field">
        <label>Imagen</label>
        <ThumbnailField
          :existing-thumbnail-id="activity?.thumbnailId"
          :invalid="submitted && missingThumbnail"
          @update:file="pickedFile = $event"
        />
        <small v-if="submitted && missingThumbnail" class="form__error"
          >La imagen es obligatoria.</small
        >
        <small v-if="uploadError" class="form__error">{{ uploadError }}</small>
      </div>
    </form>

    <template #footer>
      <Button
        label="Cancelar"
        text
        severity="secondary"
        :disabled="saving || uploading"
        @click="close"
      />
      <Button label="Guardar" :loading="saving || uploading" @click="save" />
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
  color: var(--ca-coral);
  font-size: 12.5px;
}
</style>
