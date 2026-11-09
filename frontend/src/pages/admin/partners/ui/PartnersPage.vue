<script setup lang="ts">
import type { Partner } from '@/entities/partner'
import { formatDate } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColumnFilterDate, ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ExternalLink } from '@/shared/ui/external-link'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'
import { deleteAction, editAction, RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { usePartnersAdmin } from '../model/use-partners-admin'
import PartnerFormDialog from './PartnerFormDialog.vue'

const { table, dialog, saving, save, confirmRemove } = usePartnersAdmin()
const { visible, editing, openCreate, openEdit } = dialog

function partnerActions(partner: Partner): RowAction[] {
  return [editAction(() => void openEdit(partner)), deleteAction(() => confirmRemove(partner))]
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.partners.header.title')"
      :subtitle="$t('pages.admin.partners.header.subtitle')"
    >
      <template #actions>
        <ActionButton
          data-testid="admin-partners-new"
          :label="$t('pages.admin.partners.newPartner')"
          icon="plus"
          type="primary"
          @click="openCreate"
        />
      </template>
    </AdminPageHeader>

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.partners.empty.none')"
      :error-text="$t('pages.admin.partners.empty.error')"
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
          <TablePrimaryCell :title="row.name">
            <template #leading>
              <ListThumbnail size="table" fit="contain" :thumbnail-id="row.thumbnailId" />
            </template>
          </TablePrimaryCell>
        </template>
      </el-table-column>
      <el-table-column
        prop="tier"
        :label="$t('pages.admin.partners.columns.tier')"
        width="110"
        align="right"
        sortable="custom"
      >
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('tier').value"
            data-testid="admin-partners-tier-search"
            :label="$t('pages.admin.partners.columns.tier')"
            :placeholder="$t('pages.admin.partners.columns.tier')"
            input-type="number"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <span data-testid="admin-partners-tier">{{ row.tier }}</span>
        </template>
      </el-table-column>
      <el-table-column
        prop="website"
        :label="$t('pages.admin.partners.columns.website')"
        min-width="200"
        sortable="custom"
      >
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('website').value"
            :label="$t('pages.admin.partners.columns.website')"
            :placeholder="$t('pages.admin.partners.search.website')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ExternalLink
            v-if="row.website"
            data-testid="admin-partners-website"
            :href="row.website"
          />
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column
        prop="fromDate"
        :label="$t('pages.admin.partners.columns.fromDate')"
        width="140"
        sortable="custom"
      >
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('fromDate').value"
            :label="$t('pages.admin.partners.columns.fromDate')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ formatDate(row.fromDate) }}</template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions :actions="partnerActions(row)" />
        </template>
      </el-table-column>
    </DataTable>

    <PartnerFormDialog
      v-model:visible="visible"
      :partner="editing"
      :saving="saving"
      @submit="save"
    />
  </div>
</template>
