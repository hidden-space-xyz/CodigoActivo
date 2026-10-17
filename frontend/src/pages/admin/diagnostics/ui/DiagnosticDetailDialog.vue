<script setup lang="ts">
import { formatDateTimeSeconds } from '@/shared/lib/date'
import { DataState } from '@/shared/ui/data-state'

import { levelLabelKey, levelTagType } from '../lib/levels'
import type { DiagnosticDetail } from '../model/types'

/** Opens the dialog of the entry being loaded or shown. */
const visible = defineModel<boolean>('visible', { required: true })

defineProps<{
  /** Entry with its exception, once loaded. */
  entry: DiagnosticDetail | null
  /** The entry is being loaded. */
  loading: boolean
  /** The entry could not be loaded, for example because it is past its retention. */
  failed: boolean
}>()
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="$t('pages.admin.diagnostics.detail.title')"
    width="min(860px, 94vw)"
    append-to-body
  >
    <DataState
      :loading="loading"
      :error="failed"
      :empty="!entry"
      :error-text="$t('pages.admin.diagnostics.detail.error')"
    >
      <dl v-if="entry" class="detail">
        <dt>{{ $t('pages.admin.diagnostics.columns.occurredAt') }}</dt>
        <dd>{{ formatDateTimeSeconds(entry.occurredAt) }}</dd>
        <dt>{{ $t('pages.admin.diagnostics.columns.level') }}</dt>
        <dd>
          <el-tag :type="levelTagType(entry.level)" size="small" disable-transitions>
            {{ $t(levelLabelKey(entry.level)) }}
          </el-tag>
        </dd>
        <dt>{{ $t('pages.admin.diagnostics.detail.category') }}</dt>
        <dd class="mono">{{ entry.category }}</dd>
        <template v-if="entry.eventName">
          <dt>{{ $t('pages.admin.diagnostics.detail.event') }}</dt>
          <dd class="mono">{{ entry.eventName }}</dd>
        </template>
        <template v-if="entry.traceId">
          <dt>{{ $t('pages.admin.diagnostics.columns.trace') }}</dt>
          <dd class="mono">{{ entry.traceId }}</dd>
        </template>
        <dt>{{ $t('pages.admin.diagnostics.columns.message') }}</dt>
        <dd>{{ entry.message }}</dd>
      </dl>
      <template v-if="entry?.properties">
        <h3 class="section">{{ $t('pages.admin.diagnostics.detail.properties') }}</h3>
        <pre class="block">{{ entry.properties }}</pre>
      </template>
      <h3 class="section">{{ $t('pages.admin.diagnostics.detail.exception') }}</h3>
      <pre v-if="entry?.exception" class="block">{{ entry.exception }}</pre>
      <p v-else class="none">{{ $t('pages.admin.diagnostics.detail.noException') }}</p>
    </DataState>
  </el-dialog>
</template>

<style scoped>
.detail {
  display: grid;
  grid-template-columns: max-content minmax(0, 1fr);
  gap: 8px 16px;
  margin: 0;
}

.detail dt {
  color: var(--ca-text-muted);
  font-size: 13px;
}

.detail dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.mono {
  font-family: var(--ca-font-mono);
  font-size: 12px;
}

.section {
  margin: 20px 0 8px;
  font-size: 14px;
}

.block {
  max-height: 40vh;
  margin: 0;
  padding: 12px;
  overflow: auto;
  font-family: var(--ca-font-mono);
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  background: var(--ca-surface-2);
  border: 1px solid var(--ca-border);
  border-radius: var(--ca-radius-sm);
}

.none {
  color: var(--ca-text-dim);
}
</style>
