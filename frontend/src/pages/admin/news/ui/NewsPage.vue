<script setup lang="ts">
import { formatDateTime } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColumnFilterDate, ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'

import { useNewsAdmin } from '../model/use-news-admin'
import NewsFormDialog from './NewsFormDialog.vue'

const { table, dialog, saving, featuring, save, feature, confirmRemove } = useNewsAdmin()
const { visible, editing, loading, openCreate, openEdit } = dialog
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
    >
      <el-table-column :label="$t('common.image')" width="110">
        <template #default="{ row }">
          <ListThumbnail :thumbnail-id="row.thumbnailId" :alt="row.title" style="width: 88px" />
        </template>
      </el-table-column>
      <el-table-column prop="title" min-width="220" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('title').value"
            :label="$t('pages.admin.news.columns.title')"
            :placeholder="$t('pages.admin.news.search.title')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <span class="title-cell">
            {{ row.title }}
            <el-tag v-if="row.featured" type="warning">
              {{ $t('pages.admin.news.featured') }}
            </el-tag>
          </span>
        </template>
      </el-table-column>
      <el-table-column prop="subtitle" min-width="240" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('subtitle').value"
            :label="$t('pages.admin.news.columns.subtitle')"
            :placeholder="$t('pages.admin.news.search.subtitle')"
            @apply="table.onFilter"
          />
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" sortable="custom" width="200">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('created').value"
            :label="$t('pages.admin.news.columns.created')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column
        :label="$t('common.actions')"
        width="160"
        align="center"
        :fixed="table.actionsFixed.value"
      >
        <template #default="{ row }">
          <div class="ca-row-actions">
            <ActionButton
              :icon="row.featured ? 'star-fill' : 'star'"
              type="warning"
              text
              circle
              :aria-label="
                row.featured ? $t('pages.admin.news.featured') : $t('pages.admin.news.feature')
              "
              :disabled="row.featured || featuring"
              :class="{ 'is-featured': row.featured }"
              @click="feature(row)"
            />
            <ActionButton
              icon="pencil"
              type="success"
              text
              circle
              :aria-label="$t('common.edit')"
              :disabled="loading"
              @click="openEdit(row)"
            />
            <ActionButton
              icon="trash"
              type="danger"
              text
              circle
              :aria-label="$t('common.delete')"
              @click="confirmRemove(row)"
            />
          </div>
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

<style scoped>
.title-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.ca-row-actions :deep(.is-featured .el-icon) {
  color: var(--ca-action-yellow);
}
</style>
