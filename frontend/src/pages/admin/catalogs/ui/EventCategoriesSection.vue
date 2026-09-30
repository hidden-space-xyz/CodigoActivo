<script setup lang="ts">
import { ActionButton } from '@/shared/ui/action-button'
import { ColorTag } from '@/shared/ui/color-tag'
import { ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'

import { useEventCategoriesAdmin } from '../model/use-event-categories-admin'
import CatalogSection from './CatalogSection.vue'
import EventCategoryFormDialog from './EventCategoryFormDialog.vue'

const { table, dialog, saving, save, confirmRemove } = useEventCategoriesAdmin()
const { visible, editing, openCreate, openEdit } = dialog
</script>

<template>
  <CatalogSection
    :title="$t('pages.admin.catalogs.eventCategories.title')"
    :new-label="$t('pages.admin.catalogs.eventCategories.newButton')"
    @create="openCreate"
  >
    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.catalogs.eventCategories.empty.none')"
      :error-text="$t('pages.admin.catalogs.eventCategories.empty.error')"
    >
      <el-table-column prop="name" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            :label="$t('common.name')"
            :placeholder="$t('pages.admin.catalogs.eventCategories.search.name')"
            @apply="table.onFilter"
          />
        </template>
      </el-table-column>
      <el-table-column prop="color" sortable="custom" width="160">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('color').value"
            :label="$t('entities.eventCategory.fields.color')"
            :placeholder="$t('pages.admin.catalogs.eventCategories.search.color')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ColorTag :value="row.name" :color="row.color" />
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="120" align="center">
        <template #default="{ row }">
          <div class="ca-row-actions">
            <ActionButton
              icon="pencil"
              type="success"
              text
              circle
              :aria-label="$t('common.edit')"
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

    <EventCategoryFormDialog
      v-model:visible="visible"
      :category="editing"
      :saving="saving"
      @submit="save"
    />
  </CatalogSection>
</template>
