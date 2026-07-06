<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button, ColorTag, RichTextEditor } from '@/shared/ui'
import ColorPicker from 'primevue/colorpicker'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'

import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import { useCreateEventCategoryType, useEventCategoryTypesList } from '@/entities/catalog'
import type {
  CreateEventRequest,
  EventCategoryTypeResponse,
  EventResponse,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { EMPTY_DOC_JSON, getErrorMessage, parseDateOnly, toDateOnly } from '@/shared/lib'

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
const {
  pickedFile,
  uploading,
  uploadError,
  missingThumbnail,
  reset: resetThumbnail,
  resolveThumbnailId,
} = useThumbnailUpload(() => props.event?.thumbnailId)

const categoriesQuery = useEventCategoryTypesList()
const categoryOptions = computed<EventCategoryTypeResponse[]>(
  () => categoriesQuery.data.value ?? [],
)
const categoriesMissing = computed(() => form.categoryIds.length === 0)

const createCategory = useCreateEventCategoryType()
const catDialogVisible = ref(false)
const catSubmitted = ref(false)
const creatingCat = createCategory.isPending
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

function submitNewCategory(): void {
  catSubmitted.value = true
  catError.value = ''
  if (!newCat.name.trim()) return
  createCategory.mutate(
    { name: newCat.name.trim(), color: newCatHex.value },
    {
      // The mutation refreshes the category list before settling, so the new id is selectable.
      onSuccess: (created) => {
        if (created.id && !form.categoryIds.includes(created.id)) form.categoryIds.push(created.id)
        catDialogVisible.value = false
      },
      onError: (error) => {
        catError.value = getErrorMessage(error, 'No se pudo crear la categoría.')
      },
    },
  )
}

function parse(value?: string | null): Date | null {
  return value ? new Date(value) : null
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
const signupAfterEventEnd = computed(
  () =>
    !!form.signupStartsAt &&
    !!form.eventEndsAt &&
    toDateOnly(form.signupStartsAt) > toDateOnly(form.eventEndsAt),
)
const datesValid = computed(
  () =>
    !eventStartMissing.value &&
    !eventEndMissing.value &&
    !eventOrderInvalid.value &&
    !signupStartMissing.value &&
    !signupEndMissing.value &&
    !signupOrderInvalid.value &&
    !signupAfterEventEnd.value,
)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    resetThumbnail()
    form.title = props.event?.title ?? ''
    form.subtitle = props.event?.subtitle ?? ''
    form.description = props.event?.description ?? ''
    form.categoryIds = (props.event?.categories ?? [])
      .map((cat) => cat.categoryTypeId)
      .filter((id): id is string => !!id)
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
  const thumbnailId = await resolveThumbnailId()
  if (!thumbnailId) return
  emit('submit', {
    title: form.title.trim(),
    subtitle: form.subtitle.trim(),
    description: form.description.trim() ? form.description : EMPTY_DOC_JSON,
    categoryTypeIds: form.categoryIds,
    eventStartsAt: toDateOnly(eventStartsAt),
    eventEndsAt: toDateOnly(eventEndsAt),
    signupStartsAt: signupStartsAt.toISOString(),
    signupEndsAt: signupEndsAt.toISOString(),
    thumbnailId,
  } satisfies CreateEventRequest)
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
        <InputText
          v-model="form.title"
          :maxlength="200"
          :invalid="submitted && !form.title.trim()"
          fluid
        />
      </div>
      <div class="form__field">
        <label>Subtítulo</label>
        <InputText
          v-model="form.subtitle"
          :maxlength="300"
          :invalid="submitted && !form.subtitle.trim()"
          fluid
        />
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
            :invalid="submitted && (signupStartMissing || signupAfterEventEnd)"
            fluid
          />
          <small v-if="submitted && signupStartMissing" class="form__error"
            >La apertura de inscripción es obligatoria.</small
          >
          <small v-else-if="submitted && signupAfterEventEnd" class="form__error"
            >La inscripción debe abrir antes de que termine el evento.</small
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
    <form class="form" @submit.prevent="submitNewCategory">
      <div class="form__field">
        <label>Nombre</label>
        <InputText
          v-model="newCat.name"
          :maxlength="120"
          :invalid="catSubmitted && !newCat.name.trim()"
          fluid
        />
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
      <Button label="Crear" :loading="creatingCat" @click="submitNewCategory" />
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
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}
</style>
