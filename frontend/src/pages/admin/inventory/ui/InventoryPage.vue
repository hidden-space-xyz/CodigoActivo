<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import type { InventoryCategorySummary } from '@/entities/inventory'
import { formatNumber } from '@/shared/lib/number'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { DataTable } from '@/shared/ui/data-table'
import { deleteAction, editAction, RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { useInventoryAdmin } from '../model/use-inventory-admin'
import InventoryCategoryDialog from './InventoryCategoryDialog.vue'

const { t } = useI18n()
const router = useRouter()
const { table, dialog, saving, save, confirmRemove } = useInventoryAdmin()
const { visible, editing, loading, openCreate, openEdit } = dialog

function categoryActions(category: InventoryCategorySummary): RowAction[] {
  return [
    {
      key: 'open',
      label: t('pages.admin.inventory.open'),
      icon: 'list',
      inline: true,
      run: () => {
        void router.push({ name: 'admin-inventory-category', params: { categoryId: category.id } })
      },
    },
    editAction(() => void openEdit(category), loading.value),
    deleteAction(() => confirmRemove(category)),
  ]
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.inventory.header.title')"
      :subtitle="$t('pages.admin.inventory.header.subtitle')"
    >
      <template #actions>
        <ActionButton
          data-testid="admin-inventory-new"
          :label="$t('pages.admin.inventory.newCategory')"
          icon="plus"
          type="primary"
          @click="openCreate"
        />
      </template>
    </AdminPageHeader>

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.inventory.empty.none')"
      :error-text="$t('pages.admin.inventory.empty.error')"
      :search="{ key: 'name', label: $t('common.searchByName') }"
    >
      <el-table-column
        prop="name"
        :label="$t('common.name')"
        min-width="240"
        sortable="custom"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #default="{ row }">
          <RouterLink
            class="category-link"
            data-testid="admin-inventory-category-link"
            :to="{ name: 'admin-inventory-category', params: { categoryId: row.id } }"
          >
            <TablePrimaryCell :title="row.name" />
          </RouterLink>
        </template>
      </el-table-column>
      <el-table-column
        prop="entryCount"
        :label="$t('pages.admin.inventory.columns.entryCount')"
        width="110"
        align="right"
        sortable="custom"
      >
        <template #default="{ row }">{{ formatNumber(row.entryCount) }}</template>
      </el-table-column>
      <el-table-column
        prop="total"
        :label="$t('pages.admin.inventory.columns.total')"
        width="110"
        align="right"
        sortable="custom"
      >
        <template #default="{ row }">
          <span data-testid="admin-inventory-total">{{ formatNumber(row.total) }}</span>
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions
            :actions="categoryActions(row)"
            :menu-label="$t('table.moreActionsFor', { name: row.name })"
          />
        </template>
      </el-table-column>
    </DataTable>

    <InventoryCategoryDialog
      v-model:visible="visible"
      :category="editing"
      :saving="saving"
      @submit="save"
    />
  </div>
</template>

<style scoped>
.category-link {
  display: block;
  color: inherit;
  text-decoration: none;
}

.category-link:hover {
  color: var(--ca-azure-ink);
}
</style>
