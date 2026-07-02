<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import {
  AdminPageHeader,
  AppButton as Button,
  ColumnSearch,
  ListThumbnail,
  RichTextEditor,
} from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Tag from 'primevue/tag'

import { deleteThumbnail, ThumbnailField, uploadThumbnail } from '@/entities/file'
import type { ContentController, ContentItem, ContentRequest } from '../model/use-content-entity'
import { EMPTY_DOC_JSON, formatDateTime, getErrorMessage, useCrudFeedback } from '@/shared/lib'

const props = defineProps<{
  title: string
  subtitle: string
  newLabel: string
  entityLabel: string
  controller: ContentController
}>()

const feedback = useCrudFeedback()
const confirm = useConfirm()

const table = computed(() => props.controller.table)

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
    description: form.description.trim() ? form.description : EMPTY_DOC_JSON,
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

function onFeature(item: ContentItem): void {
  if (!item.id || item.featured) return
  props.controller.feature.mutate(item.id, {
    onSuccess: () => feedback.success('Destacado actualizado.'),
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

    <DataTable
      lazy
      :value="table.items.value"
      :total-records="table.total.value"
      :loading="table.loading.value"
      data-key="id"
      striped-rows
      paginator
      :rows="table.rows.value"
      :first="table.first.value"
      :rows-per-page-options="[25, 50, 100]"
      :sort-field="table.sortField.value"
      :sort-order="table.sortOrder.value"
      removable-sort
      @page="table.onPage"
      @sort="table.onSort"
    >
      <template #empty>
        <span v-if="table.isError.value">No se pudieron cargar los registros.</span>
        <span v-else>Aún no hay registros.</span>
      </template>

      <Column header="Imagen" style="width: 110px">
        <template #body="{ data }">
          <ListThumbnail :thumbnail-id="data.thumbnailId" :alt="data.title" style="width: 88px" />
        </template>
      </Column>
      <Column field="title" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('title').value"
            label="Título"
            placeholder="Buscar título"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">
          <span class="title-cell">
            {{ data.title }}
            <Tag v-if="controller.canFeature && data.featured" value="Destacado" severity="warn" />
          </span>
        </template>
      </Column>
      <Column field="subtitle" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('subtitle').value"
            label="Subtítulo"
            placeholder="Buscar subtítulo"
            @apply="table.onFilter"
          />
        </template>
      </Column>
      <Column field="createdAt" header="Creado" sortable style="width: 200px">
        <template #body="{ data }">{{ formatDateTime(data.createdAt) }}</template>
      </Column>
      <Column header="Acciones" style="width: 170px">
        <template #body="{ data }">
          <div class="row-actions">
            <Button
              v-if="controller.canFeature"
              :icon="data.featured ? 'pi pi-star-fill' : 'pi pi-star'"
              text
              rounded
              :aria-label="data.featured ? 'Destacado' : 'Destacar'"
              :disabled="data.featured || controller.feature.isPending.value"
              :class="{ 'is-featured': data.featured }"
              @click="onFeature(data)"
            />
            <Button icon="pi pi-pencil" text rounded aria-label="Editar" @click="openEdit(data)" />
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

    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="editing ? `Editar ${entityLabel}` : newLabel"
      :style="{ width: '94vw', maxWidth: '920px' }"
      :content-style="{ maxHeight: '78vh' }"
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

.title-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.is-featured:deep(.p-button-icon) {
  color: var(--ca-amber);
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
