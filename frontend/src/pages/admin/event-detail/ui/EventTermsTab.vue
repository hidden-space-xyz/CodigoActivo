<script setup lang="ts">
import { computed } from 'vue'

import type { EventTermsSummary } from '@/entities/event'
import { fullName } from '@/entities/user'
import { formatDateTime } from '@/shared/lib/date'
import { ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'

import { useEventTermsDecisions } from '../model/use-event-terms-decisions'

const props = defineProps<{
  /** Event whose terms decisions are listed. */
  eventId: string
  /** Terms documents the event links, offered in the document filter. */
  documents: readonly EventTermsSummary[]
  /** Whether this tab is selected; decisions are only fetched while it is. */
  active: boolean
}>()

const { table, signature, openSignature } = useEventTermsDecisions(
  () => props.eventId,
  () => props.active,
)

const documentOptions = computed(() =>
  props.documents.map((document) => ({ label: document.name, value: document.id })),
)
</script>

<template>
  <div>
    <p class="lead">{{ $t('pages.admin.eventDetail.terms.lead') }}</p>
    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.eventDetail.terms.empty')"
      :error-text="$t('pages.admin.eventDetail.terms.error')"
    >
      <el-table-column prop="lastName" min-width="220" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('person').value"
            :label="$t('pages.admin.eventDetail.terms.columns.person')"
            :placeholder="$t('pages.admin.eventDetail.terms.columns.searchPerson')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <div class="person">
            <span class="person__name">{{ fullName(row) }}</span>
            <span v-if="row.email" class="person__email">{{ row.email }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="document" min-width="200" sortable="custom">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('document').value"
            :label="$t('pages.admin.eventDetail.terms.columns.document')"
            :options="documentOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ row.documentName }}</template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.eventDetail.terms.columns.decision')" width="130">
        <template #default="{ row }">
          <el-tag :type="row.accepted ? 'success' : 'danger'">
            {{
              row.accepted
                ? $t('pages.admin.eventDetail.terms.accepted')
                : $t('pages.admin.eventDetail.terms.rejected')
            }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column
        prop="decidedAt"
        :label="$t('pages.admin.eventDetail.terms.columns.decidedAt')"
        min-width="170"
        sortable="custom"
      >
        <template #default="{ row }">{{ formatDateTime(row.decidedAt) }}</template>
      </el-table-column>
      <el-table-column :label="$t('pages.admin.eventDetail.terms.columns.signature')" width="190">
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

.person {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.person__name {
  font-weight: 600;
}

.person__email {
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.signature-thumb {
  display: block;
  width: 150px;
  height: 50px;
  padding: 2px;
  background: var(--ca-signature-paper);
  border: 1px solid var(--ca-border);
  border-radius: 8px;
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
