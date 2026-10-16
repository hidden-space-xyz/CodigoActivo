<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import type { NewsSummary } from '@/entities/news-item'
import { formatDateTime } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColumnFilterDate } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { useNewsAdmin } from '../model/use-news-admin'
import NewsFormDialog from './NewsFormDialog.vue'

const { t } = useI18n()
const { table, dialog, saving, featuring, save, feature, confirmRemove } = useNewsAdmin()
const { visible, editing, loading, openCreate, openEdit } = dialog

function newsActions(newsItem: NewsSummary): RowAction[] {
  return [
    {
      key: 'edit',
      label: t('common.edit'),
      icon: 'pencil',
      inline: true,
      disabled: loading.value,
      run: () => {
        void openEdit(newsItem)
      },
    },
    {
      key: 'feature',
      label: t('pages.admin.news.feature'),
      icon: 'star',
      disabled: newsItem.featured || featuring.value,
      ...(newsItem.featured ? { reason: t('pages.admin.news.alreadyFeatured') } : {}),
      run: () => {
        feature(newsItem)
      },
    },
    {
      key: 'delete',
      label: t('common.delete'),
      icon: 'trash',
      danger: true,
      divided: true,
      run: () => {
        confirmRemove(newsItem)
      },
    },
  ]
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.news.header.title')"
      :subtitle="$t('pages.admin.news.header.subtitle')"
    >
      <template #actions>
        <ActionButton
          :label="$t('pages.admin.news.newLabel')"
          icon="plus"
          type="primary"
          :disabled="loading"
          @click="openCreate"
        />
      </template>
    </AdminPageHeader>

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.news.empty.none')"
      :error-text="$t('pages.admin.news.empty.error')"
      :search="{ key: 'search', label: $t('common.searchByTitleOrSubtitle') }"
    >
      <el-table-column
        prop="title"
        :label="$t('pages.admin.news.columns.title')"
        min-width="240"
        sortable="custom"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #default="{ row }">
          <TablePrimaryCell :title="row.title" :subtitle="row.subtitle">
            <template #leading>
              <ListThumbnail size="table" :thumbnail-id="row.thumbnailId" />
            </template>
            <template v-if="row.featured" #badges>
              <el-tag type="warning" size="small" disable-transitions>
                {{ $t('pages.admin.news.featured') }}
              </el-tag>
            </template>
          </TablePrimaryCell>
        </template>
      </el-table-column>
      <el-table-column
        prop="createdAt"
        :label="$t('pages.admin.news.columns.created')"
        sortable="custom"
        width="170"
      >
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('created').value"
            :label="$t('pages.admin.news.columns.created')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions
            :actions="newsActions(row)"
            :menu-label="$t('table.moreActionsFor', { name: row.title })"
          />
        </template>
      </el-table-column>
    </DataTable>

    <NewsFormDialog
      v-model:visible="visible"
      :news-item="editing"
      :saving="saving"
      @submit="save"
    />
  </div>
</template>
