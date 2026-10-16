<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import type { EventCategory } from '@/entities/event-category'
import { ColorTag } from '@/shared/ui/color-tag'
import { ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'

import { useEventCategoriesAdmin } from '../model/use-event-categories-admin'
import CatalogSection from './CatalogSection.vue'
import EventCategoryFormDialog from './EventCategoryFormDialog.vue'

const { t } = useI18n()
const { table, dialog, saving, save, confirmRemove } = useEventCategoriesAdmin()
const { visible, editing, openCreate, openEdit } = dialog

function rowActions(category: EventCategory): RowAction[] {
  return [
    {
      key: 'edit',
      label: t('common.edit'),
      icon: 'pencil',
      inline: true,
      run: () => void openEdit(category),
    },
    {
      key: 'delete',
      label: t('common.delete'),
      icon: 'trash',
      inline: true,
      danger: true,
      run: () => confirmRemove(category),
    },
  ]
}
</script>

<template>
  <CatalogSection
    :title="$t('pages.admin.catalogs.eventCategories.title')"
    :new-label="$t('pages.admin.catalogs.eventCategories.newButton')"
    @create="openCreate"
  >
    <DataTable
      :table="table"
      variant="flush"
      :empty-text="$t('pages.admin.catalogs.eventCategories.empty.none')"
      :error-text="$t('pages.admin.catalogs.eventCategories.empty.error')"
    >
      <el-table-column
        prop="name"
        :label="$t('common.name')"
        min-width="180"
        sortable="custom"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            :label="$t('common.name')"
            :placeholder="$t('pages.admin.catalogs.eventCategories.search.name')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <span class="category-cell">
            <ColorTag :value="row.name" :color="row.color" truncate />
            <span class="category-cell__hex">{{ row.color }}</span>
          </span>
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions :actions="rowActions(row)" />
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

<style scoped>
.category-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.category-cell__hex {
  flex: none;
  font: 12px var(--ca-font-mono);
  color: var(--ca-text-dim);
}
</style>
