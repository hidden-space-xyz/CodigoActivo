<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useQueryClient } from '@tanstack/vue-query'
import { AppButton as Button, ColorTag, RichTextEditor } from '@/shared/ui'
import ColorPicker from 'primevue/colorpicker'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'

import { ThumbnailField, uploadThumbnail } from '@/entities/file'
import { catalogQueryKeys, useEventCategoryTypesList } from '@/entities/catalog'
import { postApiEventsCategoryType } from '@/shared/api/generated/endpoints/events/events'
import type {
  CreateEventRequest,
  EventCategoryTypeResponse,
  EventResponse,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { EMPTY_DOC_JSON, getErrorMessage } from '@/shared/lib'

const props = defineProps<{ visible: boolean; event: EventResponse | null; saving: boolean }>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: CreateEventRequest | UpdateEventRequest]
}>()

interface EventForm {
  title: string
  subtitle: string
  description: string
  categoryIds: string[]
  eventStartsAt: Date | null
  eventEndsAt: Date | null
  signupStartsAt: Date | null
  signupEndsAt: Date | null
}

const form = reactive<EventForm>({
  title: '',
  subtitle: '',
  description: '',
  categoryIds: [],
  eventStartsAt: null,
  eventEndsAt: null,
  signupStartsAt: null,
  signupEndsAt: null,
})
const submitted = ref(false)
const pickedFile = ref<File | null>(null)
const uploading = ref(false)
const uploadError = ref('')

const categoriesQuery = useEventCategoryTypesList()
const queryClient = useQueryClient()
const localCategories = ref<EventCategoryTypeResponse[]>([])
const categoryOptions = computed<EventCategoryTypeResponse[]>(() => {
  const fetched = categoriesQuery.data.value ?? []
  const extra = localCategories.value.filter((cat) => !fetched.some((f) => f.id === cat.id))
  return [...fetched, ...extra]
})
const categoriesMissing = computed(() => form.categoryIds.length === 0)

const catDialogVisible = ref(false)
const catSubmitted = ref(false)
const creatingCat = ref(false)
const catError = ref('')
const newCat = reactive<{ name: string; color: string }>({ name: '', color: '6366F1' })
const newCatHex = computed(() => `#${newCat.color.replace(/^#/, '')}`)

function openNewCategory(): void {
  newCat.name = ''
  newCat.color = '6366F1'
  catSubmitted.value = false
  catError.value = ''
  catDialogVisible.value = true
}

async function createCategory(): Promise<void> {
  catSubmitted.value = true
  catError.value = ''
  if (!newCat.name.trim()) return
  creatingCat.value = true
  try {
    const response = await postApiEventsCategoryType({
      name: newCat.name.trim(),
      color: newCatHex.value,
    })
    const created = response.data
    if (created.id) {
      localCategories.value.push(created)
      if (!form.categoryIds.includes(created.id)) form.categoryIds.push(created.id)
    }
    void queryClient.invalidateQueries({ queryKey: catalogQueryKeys.eventCategoryTypes })
    catDialogVisible.value = false
  } catch (error) {
    catError.value = getErrorMessage(error, 'No se pudo crear la categoría.')
  } finally {
    creatingCat.value = false
  }
}

const missingThumbnail = computed(() => !pickedFile.value && !props.event?.thumbnailId)

function parse(value?: string | null): Date | null {
  return value ? new Date(value) : null
}

function parseDateOnly(value?: string | null): Date | null {
  if (!value) return null
  const [year, month, day] = value.slice(0, 10).split('-').map(Number)
  if (!year || !month || !day) return null
  return new Date(year, month - 1, day)
}

function toDateOnly(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

const eventStartMissing = computed(() => !form.eventStartsAt)
const eventEndMissing = computed(() => !form.eventEndsAt)
const eventOrderInvalid = computed(
  () => !!form.eventStartsAt && !!form.eventEndsAt && form.eventEndsAt < form.eventStartsAt,
)
const signupStartMissing = computed(() => !form.signupStartsAt)
const signupEndMissing = computed(() => !form.signupEndsAt)
const signupOrderInvalid = computed(
  () => !!form.signupStartsAt && !!form.signupEndsAt && form.signupEndsAt <= form.signupStartsAt,
)
const datesValid = computed(
  () =>
    !eventStartMissing.value &&
    !eventEndMissing.value &&
    !eventOrderInvalid.value &&
    !signupStartMissing.value &&
    !signupEndMissing.value &&
    !signupOrderInvalid.value,
)

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
    form.categoryIds = (props.event?.categories ?? [])
      .map((cat) => cat.categoryTypeId)
      .filter((id): id is string => !!id)
    localCategories.value = []
    form.eventStartsAt = parseDateOnly(props.event?.eventStartsAt)
    form.eventEndsAt = parseDateOnly(props.event?.eventEndsAt)
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
  if (
    !form.title.trim() ||
    !form.subtitle.trim() ||
    missingThumbnail.value ||
    !datesValid.value ||
    categoriesMissing.value
  ) {
    return
  }
  const { eventStartsAt, eventEndsAt, signupStartsAt, signupEndsAt } = form
  if (!eventStartsAt || !eventEndsAt || !signupStartsAt || !signupEndsAt) return
  const body: CreateEventRequest = {
    title: form.title.trim(),
    subtitle: form.subtitle.trim(),
    description: form.description.trim() ? form.description : EMPTY_DOC_JSON,
    categoryTypeIds: form.categoryIds,
    eventStartsAt: toDateOnly(eventStartsAt),
    eventEndsAt: toDateOnly(eventEndsAt),
    signupStartsAt: signupStartsAt.toISOString(),
    signupEndsAt: signupEndsAt.toISOString(),
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
        <label>Categorías</label>
        <div class="form__cats">
          <MultiSelect
            v-model="form.categoryIds"
            :options="categoryOptions"
            option-label="name"
            option-value="id"
            placeholder="Selecciona categorías"
            :invalid="submitted && categoriesMissing"
            filter
            class="form__cats-select"
          />
          <Button label="Nueva" icon="pi pi-plus" text size="small" @click="openNewCategory" />
        </div>
        <small v-if="submitted && categoriesMissing" class="form__error"
          >Selecciona al menos una categoría.</small
        >
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
            date-format="dd/mm/yy"
            :invalid="submitted && eventStartMissing"
            fluid
          />
          <small v-if="submitted && eventStartMissing" class="form__error"
            >La fecha de inicio es obligatoria.</small
          >
        </div>
        <div class="form__field">
          <label>Fin del evento</label>
          <DatePicker
            v-model="form.eventEndsAt"
            date-format="dd/mm/yy"
            :min-date="form.eventStartsAt ?? undefined"
            :invalid="submitted && (eventEndMissing || eventOrderInvalid)"
            fluid
          />
          <small v-if="submitted && eventEndMissing" class="form__error"
            >La fecha de fin es obligatoria.</small
          >
          <small v-else-if="submitted && eventOrderInvalid" class="form__error"
            >El fin no puede ser anterior al inicio.</small
          >
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
            :invalid="submitted && signupStartMissing"
            fluid
          />
          <small v-if="submitted && signupStartMissing" class="form__error"
            >La apertura de inscripción es obligatoria.</small
          >
        </div>
        <div class="form__field">
          <label>Cierre de inscripción</label>
          <DatePicker
            v-model="form.signupEndsAt"
            show-time
            hour-format="24"
            date-format="dd/mm/yy"
            :min-date="form.signupStartsAt ?? undefined"
            :invalid="submitted && (signupEndMissing || signupOrderInvalid)"
            fluid
          />
          <small v-if="submitted && signupEndMissing" class="form__error"
            >El cierre de inscripción es obligatorio.</small
          >
          <small v-else-if="submitted && signupOrderInvalid" class="form__error"
            >El cierre debe ser posterior a la apertura.</small
          >
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

  <Dialog
    v-model:visible="catDialogVisible"
    modal
    header="Nueva categoría"
    :style="{ width: '380px' }"
  >
    <form class="form" @submit.prevent="createCategory">
      <div class="form__field">
        <label>Nombre</label>
        <InputText v-model="newCat.name" :invalid="catSubmitted && !newCat.name.trim()" fluid />
      </div>
      <div class="form__field">
        <label>Color</label>
        <div class="form__cat-color">
          <ColorPicker v-model="newCat.color" />
          <ColorTag :value="newCat.name.trim() || 'Ejemplo'" :color="newCatHex" />
          <span class="form__hex">{{ newCatHex }}</span>
        </div>
      </div>
      <small v-if="catError" class="form__error">{{ catError }}</small>
    </form>
    <template #footer>
      <Button
        label="Cancelar"
        text
        severity="secondary"
        :disabled="creatingCat"
        @click="catDialogVisible = false"
      />
      <Button label="Crear" :loading="creatingCat" @click="createCategory" />
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

.form__cats {
  display: flex;
  align-items: center;
  gap: 8px;
}

.form__cats-select {
  flex: 1 1 auto;
  min-width: 0;
}

.form__cat-color {
  display: flex;
  align-items: center;
  gap: 12px;
}

.form__hex {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-muted);
}

.form__error {
  color: var(--ca-coral);
  font-size: 12.5px;
}
</style>
