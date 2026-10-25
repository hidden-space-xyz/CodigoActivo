<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { BLOCKED_DATA_MIN_SEARCH_LENGTH, LOCALE, MAX_SEARCH_LENGTH } from '@/shared/config'
import { formatDateTime } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { ColumnFilterDate } from '@/shared/ui/column-filter'

import { authorityLabelKey, entityLabelKey } from '../lib/labels'
import type { Candidate, Consultation } from '../model/types'

/** Text the search looks up. */
const query = defineModel<string>('query', { required: true })
/** Account the extract is about. */
const accountId = defineModel<string | null>('accountId', { required: true })
/** Days the extract covers; empty for no limit. */
const range = defineModel<(Date | null)[] | null>('range', { required: true })
/** Kinds of data the extract covers; empty for every kind. */
const entities = defineModel<string[]>('entities', { required: true })

const props = defineProps<{
  /** The open consultation. */
  consultation: Consultation
  /** Accounts the last search found. */
  candidates: readonly Candidate[]
  /** Whether a search already ran, so an empty result is told apart from none. */
  searched: boolean
  /** Shows a loading state on the search button. */
  searching: boolean
  /** Shows a loading state on the download button. */
  extracting: boolean
  /** Kinds of data an extract can be narrowed to. */
  availableEntities: readonly string[]
}>()

const emit = defineEmits<{
  /** Fired to look the typed text up. */
  search: []
  /** Fired to download the extract about the picked account. */
  download: []
  /** Fired to forget the consultation. */
  close: []
}>()

const { t } = useI18n()

const entityOptions = computed(() =>
  props.availableEntities
    .map((entity) => ({ value: entity, label: t(entityLabelKey(entity)) }))
    .sort((a, b) => a.label.localeCompare(b.label, LOCALE)),
)

function guardianName(candidate: Candidate): string {
  return props.candidates.find((other) => other.accountId === candidate.guardianId)?.name ?? ''
}

function summary(candidate: Candidate): string {
  return candidate.entries
    .map((entry) => `${t(entityLabelKey(entry.entity))}: ${entry.count}`)
    .join(' · ')
}
</script>

<template>
  <section class="panel" :aria-label="$t('pages.admin.blockedData.consultation.title')">
    <header class="panel__header">
      <div>
        <h2 class="panel__title">{{ $t('pages.admin.blockedData.consultation.title') }}</h2>
        <p class="panel__meta">
          {{
            $t('pages.admin.blockedData.consultation.meta', {
              authority: $t(authorityLabelKey(consultation.authority)),
              reference: consultation.reference,
              expiresAt: formatDateTime(consultation.expiresAt),
            })
          }}
        </p>
      </div>
      <ActionButton
        :label="$t('pages.admin.blockedData.consultation.close')"
        text
        @click="emit('close')"
      />
    </header>

    <form class="panel__search" @submit.prevent="emit('search')">
      <div class="ca-field panel__query">
        <label class="ca-field__label" for="blocked-query">{{
          $t('pages.admin.blockedData.consultation.search')
        }}</label>
        <el-input
          id="blocked-query"
          v-model="query"
          :maxlength="MAX_SEARCH_LENGTH"
          :placeholder="$t('pages.admin.blockedData.consultation.searchPlaceholder')"
          aria-describedby="blocked-query-hint"
        />
        <small id="blocked-query-hint" class="ca-field__hint">{{
          $t('pages.admin.blockedData.consultation.searchHint', {
            count: BLOCKED_DATA_MIN_SEARCH_LENGTH,
          })
        }}</small>
      </div>
      <ActionButton
        :label="$t('pages.admin.blockedData.consultation.searchAction')"
        type="primary"
        native-type="submit"
        :loading="searching"
        :disabled="query.trim().length < BLOCKED_DATA_MIN_SEARCH_LENGTH"
      />
    </form>

    <p v-if="searched && candidates.length === 0" class="panel__empty">
      {{ $t('pages.admin.blockedData.consultation.none') }}
    </p>

    <el-radio-group
      v-if="candidates.length > 0"
      v-model="accountId"
      class="panel__candidates"
      :aria-label="$t('pages.admin.blockedData.consultation.candidates')"
    >
      <el-radio
        v-for="candidate in candidates"
        :key="candidate.accountId"
        :value="candidate.accountId"
        class="panel__candidate"
      >
        <span class="panel__name">{{ candidate.name }}</span>
        <span class="panel__state">
          {{
            candidate.exists
              ? $t('pages.admin.blockedData.consultation.exists')
              : $t('pages.admin.blockedData.consultation.erased', {
                  date: formatDateTime(candidate.erasedAt),
                })
          }}
          <template v-if="candidate.guardianId">
            ·
            {{
              $t('pages.admin.blockedData.consultation.minorOf', {
                guardian: guardianName(candidate),
              })
            }}
          </template>
        </span>
        <span class="panel__entries">
          {{
            candidate.entryCount > 0
              ? summary(candidate)
              : $t('pages.admin.blockedData.consultation.noEntries')
          }}
        </span>
      </el-radio>
    </el-radio-group>

    <div v-if="candidates.length > 0" class="panel__extract">
      <ColumnFilterDate
        v-model="range"
        variant="toolbar"
        :label="$t('pages.admin.blockedData.consultation.range')"
      />
      <div class="ca-field panel__entities">
        <label class="ca-field__label" for="blocked-entities">{{
          $t('pages.admin.blockedData.consultation.entities')
        }}</label>
        <el-select
          id="blocked-entities"
          v-model="entities"
          multiple
          collapse-tags
          clearable
          :placeholder="$t('pages.admin.blockedData.consultation.allEntities')"
        >
          <el-option
            v-for="option in entityOptions"
            :key="option.value"
            :label="option.label"
            :value="option.value"
          />
        </el-select>
      </div>
      <ActionButton
        :label="$t('pages.admin.blockedData.consultation.download')"
        type="primary"
        icon="download"
        :loading="extracting"
        :disabled="!accountId"
        @click="emit('download')"
      />
    </div>
  </section>
</template>

<style scoped>
.panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px;
  margin-bottom: 24px;
  border: 1px solid var(--ca-border);
  border-radius: var(--ca-radius-lg);
  background: var(--ca-surface);
}

.panel__header {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  justify-content: space-between;
}

.panel__title {
  margin: 0;
  font-size: 1.1rem;
}

.panel__meta,
.panel__empty {
  margin: 4px 0 0;
  color: var(--ca-text-muted);
}

.panel__search,
.panel__extract {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: flex-end;
}

.panel__query {
  flex: 1 1 280px;
}

.panel__entities {
  flex: 1 1 240px;
}

.panel__candidates {
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-items: stretch;
}

.panel__candidate {
  height: auto;
  padding: 8px 0;
  white-space: normal;
}

.panel__name {
  font-weight: 600;
}

.panel__state,
.panel__entries {
  display: block;
  color: var(--ca-text-muted);
  font-size: 0.875rem;
}
</style>
