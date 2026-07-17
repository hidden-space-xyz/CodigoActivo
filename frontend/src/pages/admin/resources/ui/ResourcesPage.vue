<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
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

const { t } = useI18n()
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
      feedback.error(t('pages.admin.resources.toasts.notFound'))
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
          feedback.success(t('pages.admin.resources.toasts.updated'))
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  create.mutate(body as CreateResourceRequest, {
    onSuccess: () => {
      feedback.success(t('pages.admin.resources.toasts.created'))
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(resource: ResourceListItemResponse): void {
  requireDelete({
    header: t('pages.admin.resources.delete.header'),
    message: t('pages.admin.resources.delete.message', { title: resource.title }),
    accept: () => {
      if (!resource.id) return
      remove.mutate(resource.id, {
        onSuccess: () => feedback.success(t('pages.admin.resources.toasts.deleted')),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.resources.header.title')"
      :subtitle="$t('pages.admin.resources.header.subtitle')"
    >
      <template #actions>
        <Button
          :label="$t('pages.admin.resources.newResource')"
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
        <span v-if="table.isError.value">{{ $t('pages.admin.resources.empty.error') }}</span>
        <span v-else>{{ $t('pages.admin.resources.empty.none') }}</span>
      </template>

      <Column :header="$t('common.image')" style="width: 110px">
        <template #body="{ data }">
          <ListThumbnail :thumbnail-id="data.thumbnailId" :alt="data.title" style="width: 88px" />
        </template>
      </Column>
      <Column field="title" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('title').value"
            :label="$t('pages.admin.resources.columns.title')"
            :placeholder="$t('pages.admin.resources.search.title')"
            @apply="table.onFilter"
          />
        </template>
      </Column>
      <Column field="subtitle" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('subtitle').value"
            :label="$t('pages.admin.resources.columns.subtitle')"
            :placeholder="$t('pages.admin.resources.search.subtitle')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ data.subtitle || '—' }}</template>
      </Column>
      <Column sort-field="type" sortable style="width: 120px">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('type').value"
            :label="$t('pages.admin.resources.columns.type')"
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
            :label="$t('pages.admin.resources.columns.url')"
            :placeholder="$t('pages.admin.resources.search.url')"
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
            :label="$t('pages.admin.resources.columns.created')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ formatDateTime(data.createdAt) }}</template>
      </Column>
      <Column :header="$t('common.actions')" style="width: 170px">
        <template #body="{ data }">
          <div class="row-actions">
            <Button
              icon="pi pi-pencil"
              text
              rounded
              :aria-label="$t('common.edit')"
              :disabled="loadingDetail"
              @click="openEdit(data)"
            />
            <Button
              icon="pi pi-trash"
              text
              rounded
              severity="danger"
              :aria-label="$t('common.delete')"
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
