<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import type { InventoryColumn, InventoryEntry } from '@/entities/inventory'
import { INVENTORY_MAX_QUANTITY, INVENTORY_MIN_QUANTITY } from '@/shared/config'
import { ActionButton } from '@/shared/ui/action-button'
import { ColorTag } from '@/shared/ui/color-tag'

import { chosenOption, displayValue } from '../lib/entry-values'

const props = defineProps<{
  /** Column the cell belongs to. */
  column: InventoryColumn
  /** Entry of the row. */
  entry: InventoryEntry
  /** Disables the quantity steps while a change is saved. */
  busy: boolean
}>()

const emit = defineEmits<{
  /** Asks to add one to the quantity (`1`) or take one from it (`-1`). */
  step: [by: -1 | 1]
}>()

const { t } = useI18n()
const option = computed(() => chosenOption(props.column, props.entry))
const text = computed(() => displayValue(props.column, props.entry, t))
const quantity = computed(() => props.entry.quantity ?? 0)
</script>

<template>
  <span v-if="column.type === 'Quantity'" class="quantity" data-testid="inventory-quantity">
    <ActionButton
      icon="minus"
      circle
      size="small"
      data-testid="inventory-quantity-decrease"
      :aria-label="$t('pages.admin.inventoryCategory.decrease')"
      :disabled="busy || quantity <= INVENTORY_MIN_QUANTITY"
      @click="emit('step', -1)"
    />
    <span class="quantity__value" data-testid="inventory-quantity-value">{{ text || '—' }}</span>
    <ActionButton
      icon="plus"
      circle
      size="small"
      data-testid="inventory-quantity-increase"
      :aria-label="$t('pages.admin.inventoryCategory.increase')"
      :disabled="busy || quantity >= INVENTORY_MAX_QUANTITY"
      @click="emit('step', 1)"
    />
  </span>
  <ColorTag v-else-if="option" :value="option.name" :color="option.color" truncate />
  <span v-else :class="{ 'long-text': column.type === 'LongText' }" :title="text">
    {{ text || '—' }}
  </span>
</template>

<style scoped>
.quantity {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.quantity__value {
  min-width: 3ch;
  font-variant-numeric: tabular-nums;
  text-align: center;
}

.long-text {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
