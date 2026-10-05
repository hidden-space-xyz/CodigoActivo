<script setup lang="ts">
import { formatDate } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColumnFilterDate, ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'

import { usePartnersAdmin } from '../model/use-partners-admin'
import PartnerFormDialog from './PartnerFormDialog.vue'

const { table, dialog, saving, save, confirmRemove } = usePartnersAdmin()
const { visible, editing, openCreate, openEdit } = dialog
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.partners.header.title')"
      :subtitle="$t('pages.admin.partners.header.subtitle')"
    >
      <template #actions>
        <ActionButton
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
    >
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

    <PartnerFormDialog
      v-model:visible="visible"
      :partner="editing"
      :saving="saving"
      @submit="save"
    />
  </div>
</template>

<style scoped>
.link {
  color: var(--ca-orange-ink);
  text-decoration: none;
}

.link:hover {
  text-decoration: underline;
}
</style>
