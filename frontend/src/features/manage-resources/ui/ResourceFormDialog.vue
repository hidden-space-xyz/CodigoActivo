<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button, RichTextEditor } from '@/shared/ui'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'

import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import { useResourceTypesList } from '@/entities/catalog'
import type {
  CreateResourceRequest,
  ResourceResponse,
  ResourceTypeResponse,
  UpdateResourceRequest,
} from '@/shared/api/generated/models'
import { isRichTextBlank } from '@/shared/lib'

const props = defineProps<{
  visible: boolean
  resource: ResourceResponse | null
  saving: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: CreateResourceRequest | UpdateResourceRequest]
}>()

interface ResourceForm {
  title: string
  subtitle: string
  resourceTypeId: string
  description: string
  url: string
}

const form = reactive<ResourceForm>({
  title: '',
  subtitle: '',
  resourceTypeId: '',
  description: '',
  url: '',
})
const submitted = ref(false)
const {
  pickedFile,
  uploading,
  uploadError,
  missingThumbnail,
  reset: resetThumbnail,
  resolveThumbnailId,
} = useThumbnailUpload(() => props.resource?.thumbnailId)

const typesQuery = useResourceTypesList()
const typeOptions = computed<ResourceTypeResponse[]>(() => typesQuery.data.value ?? [])
const selectedType = computed(() =>
  typeOptions.value.find((type) => type.id === form.resourceTypeId),
)
const isExternal = computed(() => selectedType.value?.isExternal === true)

const typeMissing = computed(() => !form.resourceTypeId)
const typeUnresolved = computed(() => !!form.resourceTypeId && !selectedType.value)
const descriptionMissing = computed(
  () => !!selectedType.value && !isExternal.value && isRichTextBlank(form.description),
)
const urlMissing = computed(() => isExternal.value && !form.url.trim())
const urlInvalid = computed(
  () => isExternal.value && !!form.url.trim() && !isValidHttpUrl(form.url.trim()),
)

function isValidHttpUrl(value: string): boolean {
  if (!/^https?:\/\//i.test(value)) return false
  try {
    new URL(value)
    return true
  } catch {
    return false
  }
}

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    if (typesQuery.isError.value) void typesQuery.refetch()
    resetThumbnail()
    form.title = props.resource?.title ?? ''
    form.subtitle = props.resource?.subtitle ?? ''
    form.resourceTypeId = props.resource?.type?.id ?? ''
    form.description = props.resource?.description ?? ''
    form.url = props.resource?.url ?? ''
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
    typeMissing.value ||
    typeUnresolved.value ||
    descriptionMissing.value ||
    urlMissing.value ||
    urlInvalid.value ||
    missingThumbnail.value
  ) {
    return
  }
  const thumbnailId = await resolveThumbnailId()
  if (!thumbnailId) return
  emit('submit', {
    title: form.title.trim(),
    subtitle: form.subtitle.trim(),
    description: isExternal.value ? null : form.description,
    url: isExternal.value ? form.url.trim() : null,
    resourceTypeId: form.resourceTypeId,
    thumbnailId,
  } satisfies CreateResourceRequest)
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="resource ? 'Editar recurso' : 'Nuevo recurso'"
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
        <label>Tipo</label>
        <Select
          v-model="form.resourceTypeId"
          :options="typeOptions"
          option-label="name"
          option-value="id"
          placeholder="Selecciona un tipo"
          :loading="typesQuery.isLoading.value"
          :invalid="submitted && typeMissing"
          fluid
        />
        <small v-if="typesQuery.isError.value" class="form__error"
          >No se pudieron cargar los tipos de recurso. Cierra el diálogo y vuelve a
          intentarlo.</small
        >
        <small v-else-if="submitted && typeMissing" class="form__error"
          >Selecciona el tipo de recurso.</small
        >
      </div>
      <div v-if="selectedType && !isExternal" class="form__field">
        <label>Descripción</label>
        <RichTextEditor v-model="form.description" />
        <small v-if="submitted && descriptionMissing" class="form__error"
          >La descripción es obligatoria en los recursos internos.</small
        >
      </div>
      <div v-if="isExternal" class="form__field">
        <label>Enlace</label>
        <InputText
          v-model="form.url"
          :maxlength="500"
          placeholder="https://…"
          :invalid="submitted && (urlMissing || urlInvalid)"
          fluid
        />
        <small v-if="submitted && urlMissing" class="form__error"
          >El enlace es obligatorio en los recursos externos.</small
        >
        <small v-else-if="submitted && urlInvalid" class="form__error"
          >El enlace debe ser una URL válida que empiece por http:// o https://.</small
        >
      </div>
      <div class="form__field">
        <label>Imagen</label>
        <ThumbnailField
          :existing-thumbnail-id="resource?.thumbnailId"
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
</style>
