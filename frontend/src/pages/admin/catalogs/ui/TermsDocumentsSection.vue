<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import type { TermsDocument } from '@/entities/terms-document'
import { DataTable } from '@/shared/ui/data-table'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { useTermsDocumentsAdmin } from '../model/use-terms-documents-admin'
import CatalogSection from './CatalogSection.vue'
import TermsDocumentFormDialog from './TermsDocumentFormDialog.vue'

const { t } = useI18n()
const { table, dialog, saving, save, confirmRemove } = useTermsDocumentsAdmin()
const { visible, editing, openCreate, openEdit } = dialog

function rowActions(termsDocument: TermsDocument): RowAction[] {
  return [
    {
      key: 'edit',
      label: t('common.edit'),
      icon: 'pencil',
      inline: true,
      run: () => void openEdit(termsDocument),
    },
    {
      key: 'delete',
      label: t('common.delete'),
      icon: 'trash',
      inline: true,
      danger: true,
      run: () => confirmRemove(termsDocument),
    },
  ]
}
</script>

<template>
  <CatalogSection
    :title="$t('pages.admin.catalogs.termsDocuments.title')"
    :new-label="$t('pages.admin.catalogs.termsDocuments.newButton')"
    @create="openCreate"
  >
    <DataTable
      :table="table"
      variant="flush"
      :empty-text="$t('pages.admin.catalogs.termsDocuments.empty.none')"
      :error-text="$t('pages.admin.catalogs.termsDocuments.empty.error')"
      :search="{ key: 'name', label: $t('pages.admin.catalogs.termsDocuments.search.name') }"
    >
      <el-table-column
        prop="name"
        :label="$t('common.name')"
        min-width="180"
        sortable="custom"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #default="{ row }">
          <TablePrimaryCell :title="row.name">
            <template #badges>
              <el-tag size="small" disable-transitions>{{
                row.kind === 'Consent'
                  ? $t('pages.admin.catalogs.termsDocuments.kinds.Consent')
                  : $t('pages.admin.catalogs.termsDocuments.kinds.Conditions')
              }}</el-tag>
              <el-tag v-if="row.requiresSignature" type="info" size="small" disable-transitions>{{
                $t('pages.admin.catalogs.termsDocuments.requiresSignature')
              }}</el-tag>
            </template>
          </TablePrimaryCell>
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions :actions="rowActions(row)" />
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
