<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import { formatDateTimeSeconds } from '@/shared/lib/date'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColumnFilterDate, ColumnFilterSelect } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'

import {
  AUDIT_ACTIONS,
  AUDIT_ACTOR_TYPES,
  AUDIT_ENTITY_TYPES,
  AUDIT_OUTCOMES,
  actionLabelKey,
  actorLabel,
  actorTypeLabelKey,
  detailLabel,
  entityLabel,
  entityTypeLabelKey,
  errorCodeLabel,
  outcomeLabelKey,
  outcomeTagType,
  shortId,
} from '../lib/labels'
import type { AuditEntry } from '../model/types'
import { useAuditLog } from '../model/use-audit-log'

const i18n = useI18n()
const { t } = i18n
const hasMessage = (key: string): boolean => i18n.te(key)
const {
  table,
  extraFilters,
  filterByActor,
  filterByEntity,
  filterByIp,
  filterByTrace,
  openDiagnostics,
} = useAuditLog()

const actionOptions = AUDIT_ACTIONS.map((action) => ({
  label: t(actionLabelKey(action)),
  value: action,
})).sort((a, b) => a.label.localeCompare(b.label, 'es'))

const actorTypeOptions = AUDIT_ACTOR_TYPES.map((actorType) => ({
  label: t(actorTypeLabelKey(actorType)),
  value: actorType,
}))

const entityTypeOptions = AUDIT_ENTITY_TYPES.map((entityType) => ({
  label: t(entityTypeLabelKey(entityType)),
  value: entityType,
}))

const outcomeOptions = AUDIT_OUTCOMES.map((outcome) => ({
  label: t(outcomeLabelKey(outcome)),
  value: outcome,
}))

function entryActions(entry: AuditEntry): RowAction[] {
  return [
    {
      key: 'diagnostics',
      label: t('pages.admin.audit.openDiagnostics'),
      icon: 'list',
      inline: true,
      disabled: entry.traceId === '',
      ...(entry.traceId === '' ? { reason: t('pages.admin.audit.noTrace') } : {}),
      run: () => {
        openDiagnostics(entry.traceId)
      },
    },
  ]
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.audit.header.title')"
      :subtitle="$t('pages.admin.audit.header.subtitle')"
    />

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.audit.empty.none')"
      :error-text="$t('pages.admin.audit.empty.error')"
      :search="{ key: 'person', label: $t('pages.admin.audit.search') }"
      :extra-filters="extraFilters"
    >
      <el-table-column
        prop="occurredAt"
        :label="$t('pages.admin.audit.columns.occurredAt')"
        width="190"
        sortable="custom"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('occurred').value"
            :label="$t('pages.admin.audit.columns.occurredAt')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ formatDateTimeSeconds(row.occurredAt) }}</template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.audit.columns.actor')" min-width="200">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('actorType').value"
            :label="$t('pages.admin.audit.columns.actor')"
            :options="actorTypeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <div class="stack">
            <button
              v-if="row.actorId"
              type="button"
              class="pin"
              :aria-label="$t('pages.admin.audit.filterActor', { actor: actorLabel(row, t) })"
              @click="filterByActor(row)"
            >
              {{ actorLabel(row, t) }}
            </button>
            <span v-else class="muted">{{ actorLabel(row, t) }}</span>
            <span v-if="row.actorEmail" class="sub">{{ row.actorEmail }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.audit.columns.ip')" width="150">
        <template #default="{ row }">
          <button
            v-if="row.clientIp"
            type="button"
            class="pin mono"
            :aria-label="$t('pages.admin.audit.filterIp', { ip: row.clientIp })"
            @click="filterByIp(row.clientIp)"
          >
            {{ row.clientIp }}
          </button>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
      <el-table-column
        prop="action"
        :label="$t('pages.admin.audit.columns.action')"
        min-width="200"
        sortable="custom"
      >
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('action').value"
            :label="$t('pages.admin.audit.columns.action')"
            :options="actionOptions"
            filterable
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ $t(actionLabelKey(row.action)) }}</template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.audit.columns.entity')" min-width="220">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('entityType').value"
            :label="$t('pages.admin.audit.columns.entity')"
            :options="entityTypeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <div class="stack">
            <template v-if="row.entity">
              <button
                type="button"
                class="pin"
                :title="row.entity.id"
                :aria-label="
                  $t('pages.admin.audit.filterEntity', { entity: entityLabel(row.entity, t) })
                "
                @click="filterByEntity(row.entity)"
              >
                {{ entityLabel(row.entity, t) }}
              </button>
              <span v-if="row.entity.name" class="sub">
                {{ $t(entityTypeLabelKey(row.entity.type)) }}
              </span>
            </template>
            <span v-else class="muted">—</span>
            <span v-if="row.related.length > 0" class="related">
              <button
                v-for="related in row.related"
                :key="`${related.type}:${related.id}`"
                type="button"
                class="pin sub"
                :title="related.id"
                @click="filterByEntity(related)"
              >
                {{ $t(entityTypeLabelKey(related.type)) }} {{ shortId(related.id) }}
              </button>
            </span>
          </div>
        </template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.audit.columns.details')" min-width="180">
        <template #default="{ row }">
          <ul v-if="row.details.length > 0 || row.itemCount !== null" class="details">
            <li v-for="detail in row.details" :key="detail.name">
              {{ detailLabel(detail, t, hasMessage) }}
            </li>
            <li v-if="row.itemCount !== null">
              {{ $t('pages.admin.audit.itemCount', { count: row.itemCount }) }}
            </li>
          </ul>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.audit.columns.outcome')" min-width="200">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('outcome').value"
            :label="$t('pages.admin.audit.columns.outcome')"
            :options="outcomeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <div class="stack">
            <el-tag :type="outcomeTagType(row.outcome)" size="small" disable-transitions>
              {{ $t(outcomeLabelKey(row.outcome)) }}
            </el-tag>
            <span v-if="row.errorCode" class="sub" :title="row.errorCode">
              {{ errorCodeLabel(row.errorCode, t, hasMessage) }}
            </span>
          </div>
        </template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.audit.columns.trace')" width="150">
        <template #default="{ row }">
          <button
            v-if="row.traceId"
            type="button"
            class="pin mono trace"
            :title="row.traceId"
            :aria-label="$t('pages.admin.audit.filterTrace', { trace: row.traceId })"
            @click="filterByTrace(row.traceId)"
          >
            {{ row.traceId }}
          </button>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions
            :actions="entryActions(row)"
            :menu-label="$t('pages.admin.audit.moreActions')"
          />
        </template>
      </el-table-column>
    </DataTable>
  </div>
</template>

<style scoped>
.stack {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  min-width: 0;
}

.pin {
  max-width: 100%;
  padding: 0;
  overflow: hidden;
  color: var(--ca-orange-ink);
  font: inherit;
  text-align: left;
  text-overflow: ellipsis;
  white-space: nowrap;
  background: none;
  border: 0;
  border-radius: 3px;
  cursor: pointer;
}

.pin:hover {
  text-decoration: underline;
  text-underline-offset: 2px;
}

.pin:focus-visible {
  outline: 2px solid transparent;
  background-color: var(--ca-orange-soft);
}

.mono {
  font-family: var(--ca-font-mono);
  font-size: 12px;
}

.trace {
  display: block;
}

.sub {
  color: var(--ca-text-muted);
  font-size: 12px;
}

.related {
  display: flex;
  flex-wrap: wrap;
  gap: 2px 8px;
}

.muted {
  color: var(--ca-text-dim);
}

.details {
  margin: 0;
  padding: 0;
  list-style: none;
  font-size: 13px;
}
</style>
