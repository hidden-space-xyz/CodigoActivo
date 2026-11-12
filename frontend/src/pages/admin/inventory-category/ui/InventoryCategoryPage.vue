<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { inventoryQueries } from '@/entities/inventory'
import { BrandButton } from '@/shared/ui/brand-button'
import { DataState } from '@/shared/ui/data-state'

import InventoryEntriesTable from './InventoryEntriesTable.vue'

const props = defineProps<{
  /** Category id from the `/admin/inventory/:categoryId` route param. */
  categoryId: string
}>()

const category = useQuery(() => inventoryQueries.category(props.categoryId))

const columnsKey = computed(
  () => category.data.value?.columns.map((column) => `${column.id}:${column.type}`).join('|') ?? '',
)
</script>

<template>
  <div>
    <BrandButton variant="back" :to="{ name: 'admin-inventory' }" class="back">
      {{ $t('pages.admin.inventoryCategory.back') }}
    </BrandButton>

    <DataState
      :loading="category.isPending.value"
      :error="category.isError.value"
      :empty="!category.data.value"
      :empty-text="$t('pages.admin.inventoryCategory.notFound')"
      :error-text="$t('pages.admin.inventoryCategory.loadError')"
    >
      <InventoryEntriesTable
        v-if="category.data.value"
        :key="columnsKey"
        :category="category.data.value"
      />
    </DataState>
  </div>
</template>

<style scoped>
.back {
  margin-bottom: 16px;
}
</style>
