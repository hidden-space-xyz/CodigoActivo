<script setup lang="ts">
import { computed, ref } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'

import { deleteThumbnail } from '@/features/files/useThumbnail'
import PartnerFormDialog from '@/features/partners/PartnerFormDialog.vue'
import { usePartners } from '@/features/partners/usePartners'
import type {
  CreatePartnerRequest,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/utils/api-error'
import { formatDate } from '@/shared/utils/format'
import ListThumbnail from '@/shared/ui/components/ListThumbnail.vue'
import AdminPageHeader from '@/shared/ui/admin/AdminPageHeader.vue'
import DataState from '@/shared/ui/admin/DataState.vue'
import { useCrudFeedback } from '@/shared/ui/admin/use-crud-feedback'

const { list, create, update, remove } = usePartners()
const feedback = useCrudFeedback()
const confirm = useConfirm()

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
          feedback.success('Socio actualizado.')
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(getErrorMessage(error)),
      },
    )
    return
  }
  create.mutate(body as CreatePartnerRequest, {
    onSuccess: () => {
      feedback.success('Socio creado.')
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(getErrorMessage(error)),
  })
}

function confirmDelete(partner: PartnerResponse): void {
  confirm.require({
    header: 'Eliminar socio',
    message: `¿Seguro que quieres eliminar a "${partner.name}"? Esta acción no se puede deshacer.`,
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => {
      if (!partner.id) return
      remove.mutate(partner.id, {
        onSuccess: () => {
          feedback.success('Socio eliminado.')
          void deleteThumbnail(partner.thumbnailId)
        },
        onError: (error) => feedback.error(getErrorMessage(error)),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader title="Socios" subtitle="Empresas y entidades colaboradoras">
      <template #actions>
        <Button label="Nuevo socio" icon="pi pi-plus" @click="openCreate" />
      </template>
    </AdminPageHeader>

    <DataState
      :loading="list.isLoading.value"
      :error="list.isError.value"
      :empty="(list.data.value?.length ?? 0) === 0"
      empty-text="Aún no hay socios. Crea el primero."
    >
      <DataTable :value="list.data.value" data-key="id" striped-rows>
        <Column header="Logo" style="width: 110px">
          <template #body="{ data }">
            <ListThumbnail :thumbnail-id="data.thumbnailId" :alt="data.name" style="width: 88px" />
          </template>
        </Column>
        <Column field="name" header="Nombre" />
        <Column field="tier" header="Nivel" style="width: 90px" />
        <Column header="Sitio web">
          <template #body="{ data }">
            <a
              v-if="data.website"
              :href="data.website"
              target="_blank"
              rel="noopener"
              class="link"
              >{{ data.website }}</a
            >
            <span v-else>—</span>
          </template>
        </Column>
        <Column header="Alta">
          <template #body="{ data }">{{ formatDate(data.fromDate) }}</template>
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
  color: var(--ca-cyan);
  text-decoration: none;
}

.link:hover {
  text-decoration: underline;
}
</style>
