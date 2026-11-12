<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import type { InventoryCategory, InventoryColumn, InventoryEntry } from '@/entities/inventory'
import { INVENTORY_MAX_ENTRIES } from '@/shared/config'
import { formatNumber } from '@/shared/lib/number'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColumnFilterDate, ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { deleteAction, editAction, RowActions, type RowAction } from '@/shared/ui/row-actions'

import { entryLabel } from '../lib/entry-values'
import { useInventoryEntries } from '../model/use-inventory-entries'
import InventoryEntryDialog from './InventoryEntryDialog.vue'
import InventoryValueCell from './InventoryValueCell.vue'

const props = defineProps<{
  /** Category whose entries the table lists; the parent re-creates the table when its columns change. */
  category: InventoryCategory
}>()

const { t } = useI18n()
const { table, dialog, step, stepping, saving, save, confirmRemove } = useInventoryEntries(
  () => props.category,
)
const { visible, editing, openCreate, openEdit } = dialog

const full = computed(() => props.category.entryCount >= INVENTORY_MAX_ENTRIES)

function filterOptions(column: InventoryColumn): { label: string; value: string }[] {
  return column.type === 'Boolean'
    ? [
        { label: t('pages.admin.inventoryCategory.yes'), value: 'true' },
        { label: t('pages.admin.inventoryCategory.no'), value: 'false' },
      ]
    : column.options.map((option) => ({ label: option.name, value: option.id }))
}

function rowName(entry: InventoryEntry): string {
  return entryLabel(props.category.columns, entry, t)
}

function entryActions(entry: InventoryEntry): RowAction[] {
  return [editAction(() => void openEdit(entry)), deleteAction(() => confirmRemove(entry))]
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="category.name"
      :subtitle="
        $t(
          'pages.admin.inventoryCategory.summary',
          { count: formatNumber(category.entryCount), total: formatNumber(category.total) },
          category.entryCount,
        )
      "
    >
      <template #actions>
        <ActionButton
          data-testid="inventory-entry-new"
          :label="$t('pages.admin.inventoryCategory.newEntry')"
          :tooltip="
            full
              ? $t('pages.admin.inventoryCategory.limitReached')
              : $t('pages.admin.inventoryCategory.newEntry')
          "
          icon="plus"
          type="primary"
          :disabled="full"
          @click="openCreate"
        />
      </template>
    </AdminPageHeader>

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.inventoryCategory.empty.none')"
      :error-text="$t('pages.admin.inventoryCategory.empty.error')"
    >
      <el-table-column
        v-for="column in category.columns"
        :key="column.id"
        :prop="column.id"
        :label="column.name"
        :min-width="column.type === 'LongText' ? 240 : 150"
        :align="column.type === 'Quantity' || column.type === 'Number' ? 'right' : 'left'"
        sortable="custom"
      >
        <template #header>
          <ColumnSearch
            v-if="column.type === 'Text' || column.type === 'LongText'"
            v-model="table.columnFilter(column.id).value"
            :label="column.name"
            @apply="table.onFilter"
          />
          <ColumnSearch
            v-else-if="column.type === 'Number' || column.type === 'Quantity'"
            v-model="table.columnFilter(column.id).value"
            :label="column.name"
            input-type="number"
            @apply="table.onFilter"
          />
          <ColumnFilterDate
            v-else-if="column.type === 'Date'"
            v-model="table.columnFilter(column.id).value"
            :label="column.name"
            @apply="table.onFilter"
          />
          <ColumnFilterSelect
            v-else
            v-model="table.columnFilter(column.id).value"
            :label="column.name"
            :options="filterOptions(column)"
            :filterable="column.type === 'List'"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <InventoryValueCell
            :column="column"
            :entry="row"
            :busy="stepping"
            @step="step(row, $event)"
          />
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions
            :actions="entryActions(row)"
            :menu-label="$t('table.moreActionsFor', { name: rowName(row) })"
          />
        </template>
      </el-table-column>
    </DataTable>

    <InventoryEntryDialog
      v-model:visible="visible"
      :columns="category.columns"
      :entry="editing"
      :saving="saving"
      @submit="save"
    />
  </div>
</template>
