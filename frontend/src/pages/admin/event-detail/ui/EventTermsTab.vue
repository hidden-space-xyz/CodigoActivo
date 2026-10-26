<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import type { EventTermsSummary } from '@/entities/event'
import { fullName } from '@/entities/user'
import { formatDateTime } from '@/shared/lib/date'
import { ColumnFilterSelect } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import type { EventTermsDecision } from '../model/types'
import { useEventTermsDecisions } from '../model/use-event-terms-decisions'

const props = defineProps<{
  /** Event whose terms decisions are listed. */
  eventId: string
  /** Terms documents the event links, offered in the document filter. */
  documents: readonly EventTermsSummary[]
  /** Whether this tab is selected; decisions are only fetched while it is. */
  active: boolean
}>()

const { t } = useI18n()
const { table, signature, openSignature, withdraw } = useEventTermsDecisions(
  () => props.eventId,
  () => props.active,
)

const documentOptions = computed(() =>
  props.documents.map((document) => ({ label: document.name, value: document.id })),
)

function decisionActions(decision: EventTermsDecision): RowAction[] {
  return decision.consent && decision.accepted
    ? [
        {
          key: 'withdraw',
          label: t('pages.admin.eventDetail.terms.withdraw.action'),
          icon: 'undo',
          inline: true,
          run: () => {
            withdraw(decision)
          },
        },
      ]
    : []
}
</script>

<template>
  <div>
    <p class="lead">{{ $t('pages.admin.eventDetail.terms.lead') }}</p>
    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.eventDetail.terms.empty')"
      :error-text="$t('pages.admin.eventDetail.terms.error')"
      :search="{ key: 'person', label: $t('pages.admin.eventDetail.terms.columns.searchPerson') }"
    >
      <el-table-column
        prop="firstName"
        :label="$t('pages.admin.eventDetail.terms.columns.person')"
        min-width="240"
        sortable="custom"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #default="{ row }">
          <TablePrimaryCell :title="fullName(row)" :subtitle="row.email" />
        </template>
      </el-table-column>
      <el-table-column
        prop="document"
        :label="$t('pages.admin.eventDetail.terms.columns.document')"
        min-width="180"
        sortable="custom"
        show-overflow-tooltip
      >
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('document').value"
            :label="$t('pages.admin.eventDetail.terms.columns.document')"
            :options="documentOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <span>
            {{ row.documentName }}
            <span class="document-version">{{
              $t('pages.admin.eventDetail.terms.version', { version: row.documentVersion })
            }}</span>
          </span>
        </template>
      </el-table-column>
      <el-table-column
        :label="$t('pages.admin.eventDetail.terms.columns.decidedBy')"
        min-width="170"
        show-overflow-tooltip
      >
        <template #default="{ row }">{{
          row.decidedById === row.userId
            ? $t('pages.admin.eventDetail.terms.decidedBySelf')
            : row.decidedByName
        }}</template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.eventDetail.terms.columns.decision')" width="120">
        <template #default="{ row }">
          <el-tag
            :type="row.accepted ? 'success' : row.withdrawn ? 'warning' : 'danger'"
            size="small"
            disable-transitions
          >
            {{
              row.accepted
                ? $t('pages.admin.eventDetail.terms.accepted')
                : row.withdrawn
                  ? $t('pages.admin.eventDetail.terms.withdrawn')
                  : $t('pages.admin.eventDetail.terms.rejected')
            }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column
        prop="decidedAt"
        :label="$t('pages.admin.eventDetail.terms.columns.decidedAt')"
        width="170"
        sortable="custom"
      >
        <template #default="{ row }">{{ formatDateTime(row.decidedAt) }}</template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.eventDetail.terms.columns.signature')" width="130">
        <template #default="{ row }">
          <button
            v-if="row.signatureUrl"
            type="button"
            class="signature-thumb"
            :aria-label="$t('pages.admin.eventDetail.terms.openSignature', { name: fullName(row) })"
            @click="openSignature(row)"
          >
            <img
              :src="row.signatureUrl"
              :alt="
                $t('pages.admin.eventDetail.terms.signatureAlt', {
                  name: fullName(row),
                  document: row.documentName,
                })
              "
              loading="lazy"
            />
          </button>
          <span v-else class="no-signature">
            {{ $t('pages.admin.eventDetail.terms.noSignature') }}
          </span>
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions :actions="decisionActions(row)" />
        </template>
      </el-table-column>
    </DataTable>

    <el-dialog
      v-model="signature.visible"
      :title="
        signature.decision
          ? $t('pages.admin.eventDetail.terms.signatureOf', { name: fullName(signature.decision) })
          : ''
      "
      width="min(680px, 94vw)"
      append-to-body
    >
      <figure v-if="signature.decision" class="signature">
        <img
          :src="signature.decision.signatureUrl"
          :alt="
            $t('pages.admin.eventDetail.terms.signatureAlt', {
              name: fullName(signature.decision),
              document: signature.decision.documentName,
            })
          "
        />
        <figcaption class="signature__caption">
          {{ signature.decision.documentName }} · {{ formatDateTime(signature.decision.decidedAt) }}
        </figcaption>
      </figure>
    </el-dialog>
  </div>
</template>

<style scoped>
.lead {
  color: var(--ca-text-muted);
  font-size: 13px;
  margin-bottom: 12px;
}

.signature-thumb {
  display: block;
  width: 100px;
  height: 34px;
  padding: 2px;
  background: var(--ca-signature-paper);
  border: 1px solid var(--ca-border);
  border-radius: var(--ca-radius-sm);
  cursor: zoom-in;
}

.signature-thumb img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.no-signature {
  color: var(--ca-text-dim);
}

.document-version {
  margin-left: 6px;
  font-size: 12px;
  color: var(--ca-text-muted);
}

.signature {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.signature img {
  width: 100%;
  max-height: 60vh;
  object-fit: contain;
  background: var(--ca-signature-paper);
  border: 1px solid var(--ca-border);
  border-radius: 12px;
}

.signature__caption {
  font-size: 13px;
  color: var(--ca-text-muted);
}
</style>
