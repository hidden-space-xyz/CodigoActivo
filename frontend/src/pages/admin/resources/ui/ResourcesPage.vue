<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import type { LearningResourceSummary } from '@/entities/resource'
import { formatDateTime } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColorTag } from '@/shared/ui/color-tag'
import {
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
  toSelectOptions,
} from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ExternalLink } from '@/shared/ui/external-link'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { useResourcesAdmin } from '../model/use-resources-admin'
import ResourceFormDialog from './ResourceFormDialog.vue'

const { t } = useI18n()
const { table, types, dialog, saving, save, confirmRemove } = useResourcesAdmin()
const { visible, editing, loading, openCreate, openEdit } = dialog

const typeOptions = computed(() => toSelectOptions(types.data.value))

function rowActions(resource: LearningResourceSummary): RowAction[] {
  return [
    {
      key: 'edit',
      label: t('common.edit'),
      icon: 'pencil',
      inline: true,
      disabled: loading.value,
      run: () => {
        void openEdit(resource)
      },
    },
    {
      key: 'delete',
      label: t('common.delete'),
      icon: 'trash',
      inline: true,
      danger: true,
      run: () => {
        confirmRemove(resource)
      },
    },
  ]
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.resources.header.title')"
      :subtitle="$t('pages.admin.resources.header.subtitle')"
    >
      <template #actions>
        <ActionButton
          :label="$t('pages.admin.resources.newResource')"
          icon="plus"
          type="primary"
          :disabled="loading"
          @click="openCreate"
        />
      </template>
    </AdminPageHeader>

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.resources.empty.none')"
      :error-text="$t('pages.admin.resources.empty.error')"
      :search="{ key: 'search', label: $t('common.searchByTitleOrSubtitle') }"
    >
      <el-table-column
        prop="title"
        :label="$t('pages.admin.resources.columns.title')"
        min-width="240"
        sortable="custom"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #default="{ row }">
          <TablePrimaryCell :title="row.title" :subtitle="row.subtitle">
            <template #leading>
              <ListThumbnail size="table" :thumbnail-id="row.thumbnailId" />
            </template>
          </TablePrimaryCell>
        </template>
      </el-table-column>
      <el-table-column
        prop="type"
        :label="$t('pages.admin.resources.columns.type')"
        width="130"
        sortable="custom"
      >
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('type').value"
            :label="$t('pages.admin.resources.columns.type')"
            :options="typeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ColorTag
            v-if="row.type.name"
            variant="dot"
            :value="row.type.name"
            :color="row.type.color"
          />
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column
        prop="url"
        :label="$t('pages.admin.resources.columns.url')"
        width="220"
        sortable="custom"
      >
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('url').value"
            :label="$t('pages.admin.resources.columns.url')"
            :placeholder="$t('pages.admin.resources.search.url')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ExternalLink v-if="row.url" :href="row.url" />
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column
        prop="createdAt"
        :label="$t('pages.admin.resources.columns.created')"
        width="170"
        sortable="custom"
      >
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('created').value"
            :label="$t('pages.admin.resources.columns.created')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions :actions="rowActions(row)" />
        </template>
      </el-table-column>
    </DataTable>

    <ResourceFormDialog
      v-model:visible="visible"
      :resource="editing"
      :saving="saving"
      @submit="save"
    />
  </div>
</template>
