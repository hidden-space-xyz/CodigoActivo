<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  AdminPageHeader,
  AppButton as Button,
  ColumnFilterDate,
  ColumnSearch,
  ListThumbnail,
} from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'

import { PartnerFormDialog, usePartners } from '@/features/manage-partners'
import type {
  CreatePartnerRequest,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { formatDate, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const { t } = useI18n()
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
          feedback.success(t('pages.admin.partners.toasts.updated'))
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  create.mutate(body as CreatePartnerRequest, {
    onSuccess: () => {
      feedback.success(t('pages.admin.partners.toasts.created'))
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(partner: PartnerResponse): void {
  requireDelete({
    header: t('pages.admin.partners.delete.header'),
    message: t('pages.admin.partners.delete.message', { name: partner.name }),
    accept: () => {
      if (!partner.id) return
      remove.mutate(partner.id, {
        onSuccess: () => feedback.success(t('pages.admin.partners.toasts.deleted')),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.partners.header.title')"
      :subtitle="$t('pages.admin.partners.header.subtitle')"
    >
      <template #actions>
        <Button
          :label="$t('pages.admin.partners.newPartner')"
          icon="pi pi-plus"
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
        <span v-if="table.isError.value">{{ $t('pages.admin.partners.empty.error') }}</span>
        <span v-else>{{ $t('pages.admin.partners.empty.none') }}</span>
      </template>

      <Column :header="$t('pages.admin.partners.columns.logo')" style="width: 110px">
        <template #body="{ data }">
          <ListThumbnail :thumbnail-id="data.thumbnailId" :alt="data.name" style="width: 88px" />
        </template>
      </Column>
      <Column field="name" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            :label="$t('common.name')"
            :placeholder="$t('pages.admin.partners.search.name')"
            @apply="table.onFilter"
          />
        </template>
      </Column>
      <Column field="tier" sortable style="width: 130px">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('tier').value"
            :label="$t('pages.admin.partners.columns.tier')"
            :placeholder="$t('pages.admin.partners.columns.tier')"
            input-type="number"
            @apply="table.onFilter"
          />
        </template>
      </Column>
      <Column field="website" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('website').value"
            :label="$t('pages.admin.partners.columns.website')"
            :placeholder="$t('pages.admin.partners.search.website')"
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
      <Column field="fromDate" sortable style="width: 190px">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('fromDate').value"
            :label="$t('pages.admin.partners.columns.fromDate')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ formatDate(data.fromDate) }}</template>
      </Column>
      <Column :header="$t('common.actions')" style="width: 130px">
        <template #body="{ data }">
          <div class="row-actions">
            <Button
              icon="pi pi-pencil"
              text
              rounded
              :aria-label="$t('common.edit')"
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
