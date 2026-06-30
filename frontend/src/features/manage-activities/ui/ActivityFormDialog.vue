<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
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
  desired: Record<string, number | null>
}

const form = reactive<ActivityForm>({
  title: '',
  description: '',
  activityStartsAt: null,
  activityEndsAt: null,
  roleIds: [],
  desired: {},
})
const submitted = ref(false)
const pickedFile = ref<File | null>(null)
const uploading = ref(false)
const uploadError = ref('')

const missingThumbnail = computed(() => !pickedFile.value && !props.activity?.thumbnailId)

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
  form.desired = {}
  for (const role of props.activity?.allowedRoleTypes ?? []) {
    if (!role.roleTypeId) continue
    form.roleIds.push(role.roleTypeId)
    form.desired[role.roleTypeId] = role.desiredSignups ?? null
  }
}

watch([() => props.visible, () => props.activity], ([open]) => {
  if (!open) return
  populate()
})

function roleLabel(id: string): string {
  return props.roleTypes.find((role) => role.id === id)?.name ?? id
}

function close(): void {
  emit('update:visible', false)
}

async function save(): Promise<void> {
  submitted.value = true
  uploadError.value = ''
  if (!form.title.trim() || !form.description.trim() || missingThumbnail.value) return
  const body: CreateActivityRequest = {
    title: form.title.trim(),
    description: form.description.trim(),
    activityStartsAt: form.activityStartsAt ? form.activityStartsAt.toISOString() : null,
    activityEndsAt: form.activityEndsAt ? form.activityEndsAt.toISOString() : null,
    allowedRoleTypes: form.roleIds.map((id) => ({
      activityRoleTypeId: id,
      desiredSignups: form.desired[id] ?? null,
    })),
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
            fluid
          />
        </div>
        <div class="form__field">
          <label>Fin</label>
          <DatePicker
            v-model="form.activityEndsAt"
            show-time
            hour-format="24"
            date-format="dd/mm/yy"
            fluid
          />
        </div>
      </div>
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
      <div v-if="form.roleIds.length" class="form__desired">
        <div v-for="id in form.roleIds" :key="id" class="form__desired-row">
          <span class="form__desired-label">{{ roleLabel(id) }}</span>
          <InputNumber v-model="form.desired[id]" :min="0" placeholder="Plazas" show-buttons />
        </div>
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

.form__desired {
  display: flex;
  flex-direction: column;
  gap: 10px;
  border-top: 1px solid var(--ca-border);
  padding-top: 12px;
}

.form__desired-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.form__desired-label {
  font-size: 14px;
  color: var(--ca-text);
}

.form__error {
  color: var(--ca-coral);
  font-size: 12.5px;
}
</style>
