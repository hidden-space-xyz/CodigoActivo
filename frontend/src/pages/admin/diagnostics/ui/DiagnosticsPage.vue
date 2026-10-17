<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import { formatDateTimeSeconds } from '@/shared/lib/date'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColumnFilterDate, ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { DIAGNOSTIC_LEVELS, levelLabelKey, levelTagType } from '../lib/levels'
import type { DiagnosticEntry } from '../model/types'
import { useDiagnostics } from '../model/use-diagnostics'
import DiagnosticDetailDialog from './DiagnosticDetailDialog.vue'

const { t } = useI18n()
const { table, extraFilters, filterByTrace, openAudit, detail, openDetail } = useDiagnostics()

const levelOptions = DIAGNOSTIC_LEVELS.map((level) => ({
  label: t(levelLabelKey(level)),
  value: level,
}))

function entryActions(entry: DiagnosticEntry): RowAction[] {
  return [
    {
      key: 'detail',
      label: t('pages.admin.diagnostics.viewDetail'),
      icon: 'search',
      inline: true,
      run: () => {
        openDetail(entry)
      },
    },
    {
      key: 'audit',
      label: t('pages.admin.diagnostics.openAudit'),
      icon: 'check-square',
      disabled: entry.traceId === '',
      ...(entry.traceId === '' ? { reason: t('pages.admin.diagnostics.noTrace') } : {}),
      run: () => {
        openAudit(entry.traceId)
      },
    },
  ]
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.diagnostics.header.title')"
      :subtitle="$t('pages.admin.diagnostics.header.subtitle')"
    />

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.diagnostics.empty.none')"
      :error-text="$t('pages.admin.diagnostics.empty.error')"
      :search="{ key: 'message', label: $t('pages.admin.diagnostics.search') }"
      :extra-filters="extraFilters"
    >
      <el-table-column
        prop="occurredAt"
        :label="$t('pages.admin.diagnostics.columns.occurredAt')"
        width="190"
        sortable="custom"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('occurred').value"
            :label="$t('pages.admin.diagnostics.columns.occurredAt')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ formatDateTimeSeconds(row.occurredAt) }}</template>
      </el-table-column>
      <el-table-column
        prop="level"
        :label="$t('pages.admin.diagnostics.columns.level')"
        width="140"
        sortable="custom"
      >
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('level').value"
            :label="$t('pages.admin.diagnostics.columns.level')"
            :options="levelOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <el-tag :type="levelTagType(row.level)" size="small" disable-transitions>
            {{ $t(levelLabelKey(row.level)) }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.diagnostics.columns.source')" min-width="220">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('source').value"
            :label="$t('pages.admin.diagnostics.columns.source')"
            :placeholder="$t('pages.admin.diagnostics.searchSource')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <TablePrimaryCell
            :title="row.eventName || row.category"
            :subtitle="row.eventName ? row.category : ''"
          />
        </template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.diagnostics.columns.message')" min-width="320">
        <template #default="{ row }">
          <TablePrimaryCell :title="row.message">
            <template v-if="row.hasException" #badges>
              <el-tag type="danger" size="small" effect="plain" disable-transitions>
                {{ $t('pages.admin.diagnostics.exception') }}
              </el-tag>
            </template>
          </TablePrimaryCell>
        </template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.diagnostics.columns.trace')" width="150">
        <template #default="{ row }">
          <button
            v-if="row.traceId"
            type="button"
            class="trace"
            :title="row.traceId"
            :aria-label="$t('pages.admin.diagnostics.filterTrace', { trace: row.traceId })"
            @click="filterByTrace(row.traceId)"
          >
            {{ row.traceId }}
          </button>
          <span v-else class="none">—</span>
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions
            :actions="entryActions(row)"
            :menu-label="$t('pages.admin.diagnostics.moreActions')"
          />
        </template>
      </el-table-column>
    </DataTable>

    <DiagnosticDetailDialog
      v-model:visible="detail.visible.value"
      :entry="detail.entry.value ?? null"
      :loading="detail.loading.value"
      :failed="detail.failed.value"
    />
  </div>
</template>

<style scoped>
.trace {
  display: block;
  max-width: 100%;
  padding: 0;
  overflow: hidden;
  color: var(--ca-orange-ink);
  font-family: var(--ca-font-mono);
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
  background: none;
  border: 0;
  border-radius: 3px;
  cursor: pointer;
}

.trace:hover {
  text-decoration: underline;
  text-underline-offset: 2px;
}

.trace:focus-visible {
  outline: 2px solid transparent;
  background-color: var(--ca-orange-soft);
}

.none {
  color: var(--ca-text-dim);
}
</style>
