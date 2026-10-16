<script setup lang="ts" generic="TRow">
import { computed, nextTick, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TableInstance } from 'element-plus'

import { formatNumber } from '@/shared/lib/number'
import type { ServerTable } from '@/shared/lib/paging'
import { ActionButton } from '@/shared/ui/action-button'
import { AppIcon } from '@/shared/ui/app-icon'
import { provideTableFilters } from '@/shared/ui/column-filter'
import { SearchInput } from '@/shared/ui/search-input'

import FilterChip from './FilterChip.vue'
import type { TableFilterChip } from './table-filter-chip'

type SortChange = Parameters<ServerTable<TRow>['onSortChange']>[0]

interface DisplayedChip {
  readonly key: string | symbol
  readonly label: string
  readonly remove: () => void
}

const props = defineProps<{
  /** Server-side table state from `useServerTable`; the table, toolbar and paginator bind to it. */
  table: ServerTable<TRow>
  /** Shown once the list loaded empty with no filter active. */
  emptyText: string
  /** Shown when loading failed, with a retry button. */
  errorText: string
  /**
   * Toolbar search box bound to the text column filter `key`, which must be declared in
   * `columns`. `label` is its placeholder and accessible name. It shows no chip.
   */
  search?: { key: string; label: string }
  /** Page-owned filters outside `columns`, shown as chips and removed by «Limpiar filtros». */
  extraFilters?: readonly TableFilterChip[]
  /** `flush` drops the frame for a table that already sits in a card; `card` is the default. */
  variant?: 'card' | 'flush'
}>()

const { t } = useI18n()
const rootRef = ref<HTMLElement>()
const chipListRef = ref<HTMLElement>()
const tableRef = ref<TableInstance>()
const filters = provideTableFilters()

const pageFilters = computed<readonly TableFilterChip[]>(() => props.extraFilters ?? [])

const chips = computed<DisplayedChip[]>(() => [
  ...filters.active.value.map((filter) => ({
    key: filter.id,
    label: t('table.filterChip', { label: filter.label, value: filter.value }),
    remove: filter.clear,
  })),
  ...pageFilters.value,
])

const filtered = computed(() => props.table.hasActiveFilters.value || pageFilters.value.length > 0)

const refreshing = computed(() => props.table.loading.value && !props.table.isInitialLoading.value)

const resultCount = computed(() => {
  const { isInitialLoading, isError, items, total } = props.table
  if (isInitialLoading.value || (isError.value && items.value.length === 0)) return ''
  return t('table.results', { n: formatNumber(total.value) }, total.value)
})

function searchText(key: string): string {
  return String(props.table.filterValue(key) ?? '')
}

const emptyState = computed(() => {
  if (props.table.isError.value) return { icon: 'exclamation-triangle', text: props.errorText }
  if (filtered.value) return { icon: 'search', text: t('table.noMatches') }
  return { icon: 'list', text: props.emptyText }
})

function onSearch(key: string, value: string): void {
  props.table.columnFilter(key).value = value || null
  props.table.onFilter()
}

function clearAll(): void {
  for (const filter of [...filters.active.value]) filter.clear()
  props.table.clearFilters()
  for (const chip of [...pageFilters.value]) chip.remove()
}

async function refocus(chipIndex: number): Promise<void> {
  await nextTick()
  const removeButtons = [
    ...(chipListRef.value?.querySelectorAll<HTMLElement>('.filter-chip__remove') ?? []),
  ]
  const target =
    removeButtons[Math.min(chipIndex, removeButtons.length - 1)] ??
    rootRef.value?.querySelector<HTMLElement>('.data-table__search input') ??
    rootRef.value
  target?.focus()
}

function removeChip(chip: DisplayedChip, index: number): void {
  chip.remove()
  void refocus(index)
}

function clearAllAndRefocus(): void {
  clearAll()
  void refocus(0)
}

let applied = { field: props.table.sortField.value, order: props.table.sortOrder.value }

function onSortChange(event: SortChange): void {
  props.table.onSortChange(event)
  applied = { field: props.table.sortField.value, order: props.table.sortOrder.value }
}

watch(
  () => [props.table.sortField.value, props.table.sortOrder.value] as const,
  ([field, order]) => {
    if (field === applied.field && order === applied.order) return
    applied = { field, order }
    if (field) tableRef.value?.sort(field, order === -1 ? 'descending' : 'ascending')
    else tableRef.value?.clearSort()
  },
)
</script>

<template>
  <div
    ref="rootRef"
    class="data-table"
    :class="{ 'data-table--flush': variant === 'flush', 'data-table--refreshing': refreshing }"
    tabindex="-1"
    :aria-busy="table.loading.value"
  >
    <div class="data-table__toolbar">
      <SearchInput
        v-if="search"
        class="data-table__search"
        size="small"
        :model-value="searchText(search.key)"
        :label="search.label"
        @update:model-value="onSearch(search.key, $event)"
      />
      <div v-if="$slots.filters" class="data-table__filters">
        <slot name="filters" />
      </div>
      <div class="data-table__end">
        <span class="data-table__count" aria-live="polite">{{ resultCount }}</span>
        <slot name="actions" />
      </div>
    </div>

    <div v-if="chips.length > 0 || table.hasActiveFilters.value" class="data-table__chips">
      <ul
        v-if="chips.length > 0"
        ref="chipListRef"
        class="data-table__chip-list"
        :aria-label="$t('table.activeFilters')"
      >
        <FilterChip
          v-for="(chip, index) in chips"
          :key="chip.key"
          :label="chip.label"
          :remove-label="$t('table.removeFilter', { label: chip.label })"
          @remove="removeChip(chip, index)"
        />
      </ul>
      <ActionButton
        text
        size="small"
        class="data-table__clear"
        :label="$t('table.clearFilters')"
        @click="clearAllAndRefocus"
      />
    </div>

    <el-table ref="tableRef" v-bind="table.tableProps.value" @sort-change="onSortChange">
      <template #empty>
        <el-skeleton v-if="table.isInitialLoading.value" animated class="data-table__skeleton">
          <template #template>
            <div v-for="line in 3" :key="line" class="data-table__skeleton-row">
              <el-skeleton-item
                variant="text"
                class="data-table__skeleton-line data-table__skeleton-line--wide"
              />
              <el-skeleton-item variant="text" class="data-table__skeleton-line" />
              <el-skeleton-item
                variant="text"
                class="data-table__skeleton-line data-table__skeleton-line--end"
              />
            </div>
          </template>
        </el-skeleton>
        <div v-else class="data-table__empty" :role="table.isError.value ? 'alert' : undefined">
          <AppIcon :name="emptyState.icon" class="data-table__empty-icon" />
          <p class="data-table__empty-text">{{ emptyState.text }}</p>
          <ActionButton
            v-if="table.isError.value"
            icon="refresh"
            size="small"
            :label="$t('common.retry')"
            @click="table.refetch()"
          />
          <ActionButton
            v-else-if="filtered"
            size="small"
            :label="$t('table.clearFilters')"
            @click="clearAllAndRefocus"
          />
        </div>
      </template>
      <slot />
    </el-table>

    <div v-show="table.showPagination.value" class="data-table__footer">
      <el-pagination
        v-bind="table.paginationProps.value"
        class="data-table__pagination"
        @current-change="table.onCurrentPageChange"
        @size-change="table.onPageSizeChange"
      />
    </div>
  </div>
</template>

<style scoped>
.data-table {
  position: relative;
  isolation: isolate;
  overflow: clip;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: var(--ca-radius-lg);
  box-shadow: var(--ca-shadow-sm);
}

.data-table:focus {
  outline: none;
}

.data-table:focus-visible {
  outline: 2px solid var(--ca-orange-ink);
  outline-offset: 2px;
}

.data-table--flush {
  background: transparent;
  border: 0;
  border-radius: 0;
  box-shadow: none;
}

.data-table__toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px 12px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--ca-border-soft);
}

.data-table__toolbar :deep(.data-table__search) {
  --el-input-height: 32px;
  flex: 1 1 240px;
  max-width: 360px;
  font-size: 14px;
}

.data-table__filters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.data-table__end {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px 12px;
  margin-left: auto;
}

.data-table__end :deep(.el-button + .el-button) {
  margin-left: 0;
}

.data-table__count {
  min-height: 20px;
  font: 600 13px/20px var(--ca-font-display);
  color: var(--ca-text-muted);
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.data-table__chips {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px 10px;
  padding: 8px 16px;
  border-bottom: 1px solid var(--ca-border-soft);
}

.data-table__chip-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  min-width: 0;
  margin: 0;
  padding: 0;
  list-style: none;
}

.data-table__chips :deep(.data-table__clear) {
  flex: none;
  height: 28px;
  margin-left: 0;
  padding: 0 8px;
}

.data-table :deep(.el-table) {
  background: transparent;
  font-size: 14px;
}

.data-table :deep(.el-table__inner-wrapper::before) {
  display: none;
}

.data-table :deep(.el-table th.el-table__cell) {
  padding: 9px 0;
}

.data-table :deep(.el-table th.el-table__cell.is-leaf) {
  border-bottom: 1px solid var(--ca-border);
}

.data-table :deep(.el-table th.el-table__cell > .cell) {
  display: flex;
  align-items: center;
  gap: 2px;
  white-space: nowrap;
  line-height: 24px;
}

.data-table :deep(.el-table th.is-center > .cell) {
  justify-content: center;
}

.data-table :deep(.el-table th.is-right > .cell) {
  justify-content: flex-end;
}

.data-table :deep(.el-table .caret-wrapper) {
  flex: none;
  width: 16px;
}

.data-table :deep(.el-table .sort-caret) {
  left: 3px;
}

.data-table :deep(.el-table td.el-table__cell) {
  padding: 10px 0;
}

.data-table :deep(.el-table__body .cell) {
  line-height: 20px;
  overflow-wrap: normal;
  font-variant-numeric: tabular-nums;
}

.data-table :deep(.el-table tr > .el-table__cell:first-child > .cell) {
  padding-left: 16px;
}

.data-table :deep(.el-table tr > .el-table__cell:last-child > .cell) {
  padding-right: 16px;
}

.data-table :deep(.el-table .el-table-fixed-column--right.is-right > .cell) {
  padding-left: 0;
}

.data-table :deep(.el-table__body tr:last-child > td.el-table__cell) {
  border-bottom: 0;
}

.data-table :deep(.el-table__empty-text) {
  width: 100%;
  line-height: normal;
}

.data-table__empty {
  display: grid;
  justify-items: center;
  gap: 10px;
  padding: 36px 16px;
  color: var(--ca-text-muted);
}

.data-table__empty-icon {
  font-size: 28px;
  color: var(--ca-text-faint);
}

.data-table__empty-text {
  max-width: 48ch;
  margin: 0;
  font-size: 14px;
  line-height: 20px;
  text-wrap: balance;
}

.data-table__skeleton {
  padding: 4px 16px;
}

.data-table__skeleton-row {
  display: flex;
  align-items: center;
  gap: 24px;
  height: 48px;
  border-bottom: 1px solid var(--ca-border-soft);
}

.data-table__skeleton-row:last-child {
  border-bottom: 0;
}

.data-table__skeleton-line {
  width: 18%;
  height: 12px;
}

.data-table__skeleton-line--wide {
  width: 34%;
}

.data-table__skeleton-line--end {
  width: 10%;
  margin-left: auto;
}

.data-table__footer {
  display: flex;
  justify-content: flex-end;
  padding: 10px 16px;
  border-top: 1px solid var(--ca-border-soft);
}

.data-table--refreshing::before {
  content: '';
  position: absolute;
  inset: 0 0 auto;
  z-index: 5;
  height: 2px;
  background: linear-gradient(90deg, transparent, var(--ca-orange), transparent) 0 0 / 40% 100%
    no-repeat;
  animation: data-table-progress 1s linear infinite;
}

.data-table--refreshing :deep(.el-table__body-wrapper) {
  opacity: 0.6;
  transition: opacity 0.15s ease;
}

@keyframes data-table-progress {
  from {
    background-position: -40% 0;
  }

  to {
    background-position: 140% 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .data-table--refreshing::before {
    background: color-mix(in srgb, var(--ca-orange) 60%, transparent);
    animation: none;
  }
}

@media (max-width: 640px) {
  .data-table__toolbar :deep(.data-table__search) {
    --el-input-height: 36px;
    flex-basis: 100%;
    max-width: none;
  }

  .data-table__chips {
    flex-wrap: nowrap;
  }

  .data-table__chip-list {
    flex: 1 1 auto;
    flex-wrap: nowrap;
    overflow-x: auto;
    scrollbar-width: none;
  }

  .data-table__chips :deep(.data-table__clear) {
    height: 36px;
  }

  .data-table :deep(.el-table tr > .el-table__cell:last-child > .cell) {
    padding-right: 8px;
  }

  .data-table__footer {
    justify-content: center;
  }
}
</style>
