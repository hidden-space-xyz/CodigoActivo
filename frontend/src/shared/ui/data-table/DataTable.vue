<script setup lang="ts" generic="TRow">
import type { ServerTable } from '@/shared/lib/paging'

defineProps<{
  /** Server-side table state from `useServerTable`; the table and its paginator bind to it. */
  table: ServerTable<TRow>
  /** Shown instead of rows once the list loaded without any. */
  emptyText: string
  /** Shown instead of rows when the list failed to load. */
  errorText: string
}>()
</script>

<template>
  <div class="data-table">
    <el-table
      v-loading="table.loading.value"
      v-bind="table.tableProps.value"
      @sort-change="table.onSortChange"
    >
      <template #empty>
        <span>{{ table.isError.value ? errorText : emptyText }}</span>
      </template>
      <slot />
    </el-table>

    <el-pagination
      v-bind="table.paginationProps.value"
      class="data-table__pagination"
      @current-change="table.onCurrentPageChange"
      @size-change="table.onPageSizeChange"
    />
  </div>
</template>

<style scoped>
.data-table__pagination {
  margin-top: 14px;
  justify-content: flex-end;
}
</style>
