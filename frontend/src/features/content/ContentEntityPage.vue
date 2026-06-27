<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'

import ThumbnailField from '@/features/files/ThumbnailField.vue'
import { deleteThumbnail, uploadThumbnail } from '@/features/files/useThumbnail'
import type {
  ContentController,
  ContentItem,
  ContentRequest,
} from '@/features/content/useContentEntity'
import { getErrorMessage } from '@/shared/utils/api-error'
import { formatDateTime } from '@/shared/utils/format'
import ListThumbnail from '@/shared/ui/components/ListThumbnail.vue'
import AdminPageHeader from '@/shared/ui/admin/AdminPageHeader.vue'
import DataState from '@/shared/ui/admin/DataState.vue'
import { useCrudFeedback } from '@/shared/ui/admin/use-crud-feedback'

const props = defineProps<{
  title: string
  subtitle: string
  newLabel: string
  entityLabel: string
  controller: ContentController
}>()

const feedback = useCrudFeedback()
const confirm = useConfirm()

const dialogVisible = ref(false)
const editing = ref<ContentItem | null>(null)
const loadingDetail = ref(false)
const submitted = ref(false)
const pickedFile = ref<File | null>(null)
const uploading = ref(false)
const uploadError = ref('')
const form = reactive<{ title: string; subtitle: string; description: string }>({
  title: '',
  subtitle: '',
  description: '',
})

const saving = computed(
  () => props.controller.create.isPending.value || props.controller.update.isPending.value,
)

const missingThumbnail = computed(() => !pickedFile.value && !editing.value?.thumbnailId)

watch([dialogVisible, editing], ([open]) => {
  if (!open) return
  submitted.value = false
  pickedFile.value = null
  uploadError.value = ''
  form.title = editing.value?.title ?? ''
  form.subtitle = editing.value?.subtitle ?? ''
  form.description = editing.value?.description ?? ''
})

function openCreate(): void {
  editing.value = null
  dialogVisible.value = true
}

async function openEdit(item: ContentItem): Promise<void> {
  editing.value = item
  dialogVisible.value = true
  if (!item.id) return
  loadingDetail.value = true
  try {
    editing.value = await props.controller.fetchOne(item.id)
  } catch {
  } finally {
    loadingDetail.value = false
  }
}

async function save(): Promise<void> {
  submitted.value = true
  uploadError.value = ''
  if (!form.title.trim() || !form.subtitle.trim() || missingThumbnail.value) return
  const body: ContentRequest = {
    title: form.title.trim(),
    subtitle: form.subtitle.trim(),
    description: form.description.trim() || null,
  }
  uploading.value = true
  try {
    if (pickedFile.value) {
      body.thumbnailId = await uploadThumbnail(pickedFile.value, editing.value?.thumbnailId)
    } else if (editing.value?.thumbnailId) {
      body.thumbnailId = editing.value.thumbnailId
    }
  } catch (error) {
    uploadError.value = getErrorMessage(error, 'No se pudo subir la imagen.')
    uploading.value = false
    return
  }
  uploading.value = false

  if (editing.value?.id) {
    props.controller.update.mutate(
      { id: editing.value.id, body },
      {
        onSuccess: () => {
          feedback.success('Cambios guardados.')
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(getErrorMessage(error)),
      },
    )
    return
  }
  props.controller.create.mutate(body, {
    onSuccess: () => {
      feedback.success('Creado correctamente.')
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(getErrorMessage(error)),
  })
}

function confirmDelete(item: ContentItem): void {
  confirm.require({
    header: `Eliminar ${props.entityLabel}`,
    message: `¿Seguro que quieres eliminar "${item.title}"? Esta acción no se puede deshacer.`,
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => {
      if (!item.id) return
      props.controller.remove.mutate(item.id, {
        onSuccess: () => {
          feedback.success('Eliminado.')
          void deleteThumbnail(item.thumbnailId)
        },
        onError: (error) => feedback.error(getErrorMessage(error)),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader :title="title" :subtitle="subtitle">
      <template #actions>
        <Button :label="newLabel" icon="pi pi-plus" @click="openCreate" />
      </template>
    </AdminPageHeader>

    <DataState
      :loading="controller.list.isLoading.value"
      :error="controller.list.isError.value"
      :empty="(controller.list.data.value?.length ?? 0) === 0"
      empty-text="Aún no hay registros."
    >
      <DataTable
        :value="controller.list.data.value"
        data-key="id"
        striped-rows
        paginator
        :rows="10"
      >
        <Column header="Imagen" style="width: 110px">
          <template #body="{ data }">
            <ListThumbnail :thumbnail-id="data.thumbnailId" :alt="data.title" style="width: 88px" />
          </template>
        </Column>
        <Column field="title" header="Título" />
        <Column field="subtitle" header="Subtítulo" />
        <Column header="Creado">
          <template #body="{ data }">{{ formatDateTime(data.createdAt) }}</template>
        </Column>
        <Column header="Acciones" style="width: 130px">
          <template #body="{ data }">
            <div class="row-actions">
              <Button
                icon="pi pi-pencil"
                text
                rounded
                aria-label="Editar"
                @click="openEdit(data)"
              />
              <Button
                icon="pi pi-trash"
                text
                rounded
                severity="danger"
                aria-label="Eliminar"
                @click="confirmDelete(data)"
              />
            </div>
          </template>
        </Column>
      </DataTable>
    </DataState>

    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="editing ? `Editar ${entityLabel}` : newLabel"
      :style="{ width: '520px' }"
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
          <Textarea v-model="form.description" rows="4" auto-resize fluid />
        </div>
        <div class="form__field">
          <label>Imagen</label>
          <ThumbnailField
            :existing-thumbnail-id="editing?.thumbnailId"
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
          @click="dialogVisible = false"
        />
        <Button label="Guardar" :loading="saving || uploading || loadingDetail" @click="save" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.row-actions {
  display: flex;
  gap: 2px;
}

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
  color: var(--ca-coral);
  font-size: 12.5px;
}
</style>
