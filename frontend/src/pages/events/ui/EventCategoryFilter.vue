<script setup lang="ts">
import type { EventCategoryTag } from '@/entities/event'
import { normalizeHexColor } from '@/shared/lib'

defineProps<{
  /** Selectable categories, each shown with its color swatch. */
  categories: readonly EventCategoryTag[]
  /** Selected category id (`v-model`); an empty string means all categories. */
  modelValue: string
}>()

const emit = defineEmits<{
  /** Fired with the selected category id, or an empty string when the selection is cleared. */
  'update:modelValue': [value: string]
}>()

function onChange(value: unknown): void {
  emit('update:modelValue', typeof value === 'string' ? value : '')
}
</script>

<template>
  <el-select
    class="category-filter"
    :model-value="modelValue"
    clearable
    :placeholder="$t('pages.events.allCategories')"
    :aria-label="$t('pages.events.categoryFilter')"
    @change="onChange"
  >
    <el-option
      v-for="category in categories"
      :key="category.id"
      :label="category.name"
      :value="category.id"
    >
      <span class="category-filter__option">
        <span
          class="category-filter__swatch"
          :style="{ backgroundColor: normalizeHexColor(category.color) ?? undefined }"
        />
        {{ category.name }}
      </span>
    </el-option>
  </el-select>
</template>

<style scoped>
.category-filter__option {
  display: inline-flex;
  align-items: center;
  gap: 10px;
}

.category-filter__swatch {
  flex: none;
  width: 10px;
  height: 10px;
  border-radius: 999px;
  background-color: var(--ca-text-faint);
  box-shadow: 0 0 0 1px var(--ca-border-strong);
}
</style>
