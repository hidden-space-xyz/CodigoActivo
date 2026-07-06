<script setup lang="ts">
import { computed, ref } from 'vue'
import { AdminPageHeader, AppButton as Button, ColumnSearch, ListThumbnail } from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'

import { PartnerFormDialog, usePartners } from '@/features/manage-partners'
import type {
  CreatePartnerRequest,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { formatDate, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const { table, create, update, remove } = usePartners()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const dialogVisible = ref(false)
const selected = ref<PartnerResponse | null>(null)
const saving = computed(() => create.isPending.value || update.isPending.value)

function openCreate(): void {
  selected.value = null
  dialogVisible.value = true
}

function openEdit(partner: PartnerResponse): void {
  selected.value = partner
  dialogVisible.value = true
}

function onSubmit(body: CreatePartnerRequest | UpdatePartnerRequest): void {
  if (selected.value?.id) {
    update.mutate(
      { id: selected.value.id, body: body as UpdatePartnerRequest },
      {
        onSuccess: () => {
          feedback.success('Patrocinador actualizado.')
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  create.mutate(body as CreatePartnerRequest, {
    onSuccess: () => {
      feedback.success('Patrocinador creado.')
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(partner: PartnerResponse): void {
  requireDelete({
    header: 'Eliminar patrocinador',
    message: `¿Seguro que quieres eliminar a "${partner.name}"? Esta acción no se puede deshacer.`,
    accept: () => {
      if (!partner.id) return
      remove.mutate(partner.id, {
        onSuccess: () => feedback.success('Patrocinador eliminado.'),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader title="Patrocinadores" subtitle="Empresas y entidades colaboradoras">
      <template #actions>
        <Button label="Nuevo patrocinador" icon="pi pi-plus" @click="openCreate" />
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
        <span v-if="table.isError.value">No se pudieron cargar los patrocinadores.</span>
        <span v-else>Aún no hay patrocinadores. Crea el primero.</span>
      </template>

      <Column header="Logo" style="width: 110px">
        <template #body="{ data }">
          <ListThumbnail :thumbnail-id="data.thumbnailId" :alt="data.name" style="width: 88px" />
        </template>
      </Column>
      <Column field="name" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            label="Nombre"
            placeholder="Buscar nombre"
            @apply="table.onFilter"
          />
        </template>
      </Column>
      <Column field="tier" sortable style="width: 130px">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('tier').value"
            label="Nivel"
            placeholder="Nivel"
            input-type="number"
            @apply="table.onFilter"
          />
        </template>
      </Column>
      <Column field="website" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('website').value"
            label="Sitio web"
            placeholder="Buscar sitio"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">
          <a v-if="data.website" :href="data.website" target="_blank" rel="noopener" class="link">{{
            data.website
          }}</a>
          <span v-else>—</span>
        </template>
      </Column>
      <Column field="fromDate" header="Alta" sortable style="width: 160px">
        <template #body="{ data }">{{ formatDate(data.fromDate) }}</template>
      </Column>
      <Column header="Acciones" style="width: 130px">
        <template #body="{ data }">
          <div class="row-actions">
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

    <PartnerFormDialog
      v-model:visible="dialogVisible"
      :partner="selected"
      :saving="saving"
      @submit="onSubmit"
    />
  </div>
</template>

<style scoped>
.row-actions {
  display: flex;
  gap: 2px;
}

.link {
  color: var(--ca-orange-ink);
  text-decoration: none;
}

.link:hover {
  text-decoration: underline;
}
</style>
