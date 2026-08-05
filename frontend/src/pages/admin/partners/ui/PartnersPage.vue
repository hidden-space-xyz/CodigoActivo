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
          icon="plus"
          type="primary"
          @click="openCreate"
        />
      </template>
    </AdminPageHeader>

    <el-table
      v-bind="table.tableProps.value"
      v-loading="table.loading.value"
      @sort-change="table.onSortChange"
    >
      <template #empty>
        <span v-if="table.isError.value">{{ $t('pages.admin.partners.empty.error') }}</span>
        <span v-else>{{ $t('pages.admin.partners.empty.none') }}</span>
      </template>

      <el-table-column :label="$t('pages.admin.partners.columns.logo')" width="110">
        <template #default="{ row }">
          <ListThumbnail :thumbnail-id="row.thumbnailId" :alt="row.name" style="width: 88px" />
        </template>
      </el-table-column>
      <el-table-column prop="name" min-width="200" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            :label="$t('common.name')"
            :placeholder="$t('pages.admin.partners.search.name')"
            @apply="table.onFilter"
          />
        </template>
      </el-table-column>
      <el-table-column prop="tier" sortable="custom" width="130">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('tier').value"
            :label="$t('pages.admin.partners.columns.tier')"
            :placeholder="$t('pages.admin.partners.columns.tier')"
            input-type="number"
            @apply="table.onFilter"
          />
        </template>
      </el-table-column>
      <el-table-column prop="website" min-width="220" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('website').value"
            :label="$t('pages.admin.partners.columns.website')"
            :placeholder="$t('pages.admin.partners.search.website')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <a v-if="row.website" :href="row.website" target="_blank" rel="noopener" class="link">{{
            row.website
          }}</a>
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column prop="fromDate" sortable="custom" width="190">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('fromDate').value"
            :label="$t('pages.admin.partners.columns.fromDate')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ formatDate(row.fromDate) }}</template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="130">
        <template #default="{ row }">
          <div class="row-actions">
            <Button
              icon="pencil"
              text
              circle
              :aria-label="$t('common.edit')"
              @click="openEdit(row)"
            />
            <Button
              icon="trash"
              text
              circle
              type="danger"
              :aria-label="$t('common.delete')"
              @click="confirmDelete(row)"
            />
          </div>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-bind="table.paginationProps.value"
      class="table-pagination"
      @current-change="table.onCurrentPageChange"
      @size-change="table.onPageSizeChange"
    />

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

.table-pagination {
  margin-top: 14px;
  justify-content: flex-end;
}

.link {
  color: var(--ca-orange-ink);
  text-decoration: none;
}

.link:hover {
  text-decoration: underline;
}
</style>
