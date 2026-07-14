<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  AdminPageHeader,
  AppButton as Button,
  ColorTag,
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
  ListThumbnail,
} from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'

import { useResourceTypesList } from '@/entities/catalog'
import { ResourceFormDialog, useResourcesAdmin } from '@/features/manage-resources'
import type {
  CreateResourceRequest,
  ResourceListItemResponse,
  ResourceResponse,
  UpdateResourceRequest,
} from '@/shared/api/generated/models'
import { formatDateTime, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const { table, create, update, remove, fetchOne } = useResourcesAdmin()
const resourceTypes = useResourceTypesList()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const dialogVisible = ref(false)
const selected = ref<ResourceResponse | null>(null)
const loadingDetail = ref(false)
const saving = computed(() => create.isPending.value || update.isPending.value)

const typeOptions = computed(() =>
  (resourceTypes.data.value ?? []).map((type) => ({
    label: type.name ?? '—',
    value: type.id ?? '',
  })),
)

function openCreate(): void {
  if (loadingDetail.value) return
  selected.value = null
  dialogVisible.value = true
}

async function openEdit(resource: ResourceListItemResponse): Promise<void> {
  if (!resource.id || loadingDetail.value) return
  loadingDetail.value = true
  try {
    const detail = await fetchOne(resource.id)
    if (!detail) {
      feedback.error('Este recurso ya no existe.')
      return
    }
    selected.value = detail
    dialogVisible.value = true
  } catch (error) {
    feedback.error(error)
  } finally {
    loadingDetail.value = false
  }
}

function onSubmit(body: CreateResourceRequest | UpdateResourceRequest): void {
  if (selected.value?.id) {
    update.mutate(
      { id: selected.value.id, body: body as UpdateResourceRequest },
      {
        onSuccess: () => {
          feedback.success('Recurso actualizado.')
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  create.mutate(body as CreateResourceRequest, {
    onSuccess: () => {
      feedback.success('Recurso creado.')
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(resource: ResourceListItemResponse): void {
  requireDelete({
    header: 'Eliminar recurso',
    message: `¿Seguro que quieres eliminar "${resource.title}"?`,
    accept: () => {
      if (!resource.id) return
      remove.mutate(resource.id, {
        onSuccess: () => feedback.success('Recurso eliminado.'),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader title="Recursos" subtitle="Material formativo publicado">
      <template #actions>
        <Button
          label="Nuevo recurso"
          icon="pi pi-plus"
          :disabled="loadingDetail"
          @click="openCreate"
        />
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
        <span v-if="table.isError.value">No se pudieron cargar los recursos.</span>
        <span v-else>Aún no hay recursos.</span>
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
        <template #body="{ data }">{{ data.subtitle || '—' }}</template>
      </Column>
      <Column sort-field="type" sortable style="width: 120px">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('type').value"
            label="Tipo"
            :options="typeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">
          <ColorTag v-if="data.type?.name" :value="data.type.name" :color="data.type.color" />
          <span v-else>—</span>
        </template>
      </Column>
      <Column sort-field="url" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('url').value"
            label="Enlace"
            placeholder="Buscar enlace"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">
          <a
            v-if="data.url"
            :href="data.url"
            target="_blank"
            rel="noopener"
            class="url-cell"
            :title="data.url"
            >{{ data.url }}</a
          >
          <span v-else>—</span>
        </template>
      </Column>
      <Column field="createdAt" sortable style="width: 200px">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('created').value"
            label="Creado"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ formatDateTime(data.createdAt) }}</template>
      </Column>
      <Column header="Acciones" style="width: 170px">
        <template #body="{ data }">
          <div class="row-actions">
            <Button
              icon="pi pi-pencil"
              text
              rounded
              aria-label="Editar"
              :disabled="loadingDetail"
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

    <ResourceFormDialog
      v-model:visible="dialogVisible"
      :resource="selected"
      :saving="saving"
      @submit="onSubmit"
    />
  </div>
</template>

<style scoped>
.row-actions {
  display: flex;
  align-items: center;
  gap: 2px;
}

.url-cell {
  display: inline-block;
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: bottom;
  color: var(--ca-text-muted);
}
</style>
