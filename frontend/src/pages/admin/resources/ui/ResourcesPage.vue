<script setup lang="ts">
import { computed } from 'vue'

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
import { ListThumbnail } from '@/shared/ui/list-thumbnail'

import { useResourcesAdmin } from '../model/use-resources-admin'
import ResourceFormDialog from './ResourceFormDialog.vue'

const { table, types, dialog, saving, save, confirmRemove } = useResourcesAdmin()
const { visible, editing, loading, openCreate, openEdit } = dialog

const typeOptions = computed(() => toSelectOptions(types.data.value))
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
    >
      <el-table-column :label="$t('common.image')" width="110">
        <template #default="{ row }">
          <ListThumbnail :thumbnail-id="row.thumbnailId" :alt="row.title" style="width: 88px" />
        </template>
      </el-table-column>
      <el-table-column prop="title" min-width="180" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('title').value"
            :label="$t('pages.admin.resources.columns.title')"
            :placeholder="$t('pages.admin.resources.search.title')"
            @apply="table.onFilter"
          />
        </template>
      </el-table-column>
      <el-table-column prop="subtitle" min-width="160" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('subtitle').value"
            :label="$t('pages.admin.resources.columns.subtitle')"
            :placeholder="$t('pages.admin.resources.search.subtitle')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ row.subtitle || '—' }}</template>
      </el-table-column>
      <el-table-column prop="type" sortable="custom" width="120">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('type').value"
            :label="$t('pages.admin.resources.columns.type')"
            :options="typeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ColorTag v-if="row.type.name" :value="row.type.name" :color="row.type.color" />
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column prop="url" min-width="190" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('url').value"
            :label="$t('pages.admin.resources.columns.url')"
            :placeholder="$t('pages.admin.resources.search.url')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <a
            v-if="row.url"
            :href="row.url"
            target="_blank"
            rel="noopener"
            class="url-cell"
            :title="row.url"
            >{{ row.url }}</a
          >
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" sortable="custom" width="200">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('created').value"
            :label="$t('pages.admin.resources.columns.created')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column
        :label="$t('common.actions')"
        width="120"
        align="center"
        :fixed="table.actionsFixed.value"
      >
        <template #default="{ row }">
          <div class="ca-row-actions">
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
              text
              circle
              type="danger"
              :aria-label="$t('common.delete')"
              @click="confirmRemove(row)"
            />
          </div>
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

<style scoped>
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
