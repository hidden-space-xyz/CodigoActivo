<script setup lang="ts">
import { ActionButton } from '@/shared/ui/action-button'
import { ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'

import { useTermsDocumentsAdmin } from '../model/use-terms-documents-admin'
import CatalogSection from './CatalogSection.vue'
import TermsDocumentFormDialog from './TermsDocumentFormDialog.vue'

const { table, dialog, saving, save, confirmRemove } = useTermsDocumentsAdmin()
const { visible, editing, openCreate, openEdit } = dialog
</script>

<template>
  <CatalogSection
    :title="$t('pages.admin.catalogs.termsDocuments.title')"
    :new-label="$t('pages.admin.catalogs.termsDocuments.newButton')"
    @create="openCreate"
  >
    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.catalogs.termsDocuments.empty.none')"
      :error-text="$t('pages.admin.catalogs.termsDocuments.empty.error')"
    >
      <el-table-column prop="name" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            :label="$t('common.name')"
            :placeholder="$t('pages.admin.catalogs.termsDocuments.search.name')"
            @apply="table.onFilter"
          />
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

    <TermsDocumentFormDialog
      v-model:visible="visible"
      :terms-document="editing"
      :saving="saving"
      @submit="save"
    />
  </CatalogSection>
</template>
