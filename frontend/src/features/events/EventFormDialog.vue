<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import Button from 'primevue/button'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'

import ThumbnailField from '@/features/files/ThumbnailField.vue'
import { uploadThumbnail } from '@/features/files/useThumbnail'
import type {
  CreateEventRequest,
  EventResponse,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/utils/api-error'
import RichTextEditor from '@/shared/ui/components/RichTextEditor.vue'
import { EMPTY_DOC_JSON } from '@/shared/utils/richtext'

const props = defineProps<{ visible: boolean; event: EventResponse | null; saving: boolean }>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: CreateEventRequest | UpdateEventRequest]
}>()

interface EventForm {
  title: string
  subtitle: string
  description: string
  eventStartsAt: Date | null
  eventEndsAt: Date | null
  signupStartsAt: Date | null
  signupEndsAt: Date | null
}

const form = reactive<EventForm>({
  title: '',
  subtitle: '',
  description: '',
  eventStartsAt: null,
  eventEndsAt: null,
  signupStartsAt: null,
  signupEndsAt: null,
})
const submitted = ref(false)
const pickedFile = ref<File | null>(null)
const uploading = ref(false)
const uploadError = ref('')

const missingThumbnail = computed(() => !pickedFile.value && !props.event?.thumbnailId)

function parse(value?: string | null): Date | null {
  return value ? new Date(value) : null
}

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    pickedFile.value = null
    uploadError.value = ''
    form.title = props.event?.title ?? ''
    form.subtitle = props.event?.subtitle ?? ''
    form.description = props.event?.description ?? ''
    form.eventStartsAt = parse(props.event?.eventStartsAt)
    form.eventEndsAt = parse(props.event?.eventEndsAt)
    form.signupStartsAt = parse(props.event?.signupStartsAt)
    form.signupEndsAt = parse(props.event?.signupEndsAt)
  },
)

function close(): void {
  emit('update:visible', false)
}

async function save(): Promise<void> {
  submitted.value = true
  uploadError.value = ''
  if (!form.title.trim() || !form.subtitle.trim() || missingThumbnail.value) return
  const body: CreateEventRequest = {
    title: form.title.trim(),
    subtitle: form.subtitle.trim(),
    description: form.description.trim() ? form.description : EMPTY_DOC_JSON,
    eventStartsAt: form.eventStartsAt ? form.eventStartsAt.toISOString() : null,
    eventEndsAt: form.eventEndsAt ? form.eventEndsAt.toISOString() : null,
    signupStartsAt: form.signupStartsAt ? form.signupStartsAt.toISOString() : null,
    signupEndsAt: form.signupEndsAt ? form.signupEndsAt.toISOString() : null,
  }
  uploading.value = true
  try {
    if (pickedFile.value) {
      body.thumbnailId = await uploadThumbnail(pickedFile.value, props.event?.thumbnailId)
    } else if (props.event?.thumbnailId) {
      body.thumbnailId = props.event.thumbnailId
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
    :header="event ? 'Editar evento' : 'Nuevo evento'"
    :style="{ width: '94vw', maxWidth: '920px' }"
    :content-style="{ maxHeight: '78vh' }"
    @update:visible="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label>Título</label>
        <InputText v-model="form.title" :invalid="submitted && !form.title.trim()" fluid />
      </div>
      <div class="form__field">
        <label>Subtítulo</label>
        <InputText v-model="form.subtitle" :invalid="submitted && !form.subtitle.trim()" fluid />
      </div>
      <div class="form__field">
        <label>Descripción</label>
        <RichTextEditor v-model="form.description" />
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>Inicio del evento</label>
          <DatePicker
            v-model="form.eventStartsAt"
            show-time
            hour-format="24"
            date-format="dd/mm/yy"
            fluid
          />
        </div>
        <div class="form__field">
          <label>Fin del evento</label>
          <DatePicker
            v-model="form.eventEndsAt"
            show-time
            hour-format="24"
            date-format="dd/mm/yy"
            fluid
          />
        </div>
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>Apertura de inscripción</label>
          <DatePicker
            v-model="form.signupStartsAt"
            show-time
            hour-format="24"
            date-format="dd/mm/yy"
            fluid
          />
        </div>
        <div class="form__field">
          <label>Cierre de inscripción</label>
          <DatePicker
            v-model="form.signupEndsAt"
            show-time
            hour-format="24"
            date-format="dd/mm/yy"
            fluid
          />
        </div>
      </div>
      <div class="form__field">
        <label>Imagen</label>
        <ThumbnailField
          :existing-thumbnail-id="event?.thumbnailId"
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
