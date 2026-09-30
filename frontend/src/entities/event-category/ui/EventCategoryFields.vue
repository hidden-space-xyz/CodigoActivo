<script setup lang="ts">
import { computed } from 'vue'

import { ColorTag } from '@/shared/ui/color-tag'

import { DEFAULT_CATEGORY_COLOR, toHexColor } from '../model/category-form'

/** Name of the category; the preview tag shows it. */
const name = defineModel<string>('name', { required: true })

/** Color of the category, with or without its leading `#`. */
const color = defineModel<string>('color', { required: true })

defineProps<{
  /** Marks the name as refused. */
  invalid: boolean
}>()

const hex = computed(() => toHexColor(color.value))

function pickColor(value: string | null): void {
  color.value = value ?? DEFAULT_CATEGORY_COLOR
}
</script>

<template>
  <div class="category-fields">
    <div class="category-fields__field">
      <label for="event-category-name">{{ $t('common.name') }}</label>
      <el-input
        id="event-category-name"
        v-model="name"
        :maxlength="120"
        :class="{ 'ca-invalid': invalid }"
      />
    </div>
    <div class="category-fields__field">
      <div id="event-category-color-label" class="category-fields__label">
        {{ $t('entities.eventCategory.fields.color') }}
      </div>
      <div class="category-fields__color" role="group" aria-labelledby="event-category-color-label">
        <el-color-picker :model-value="hex" color-format="hex" @update:model-value="pickColor" />
        <ColorTag
          :value="name.trim() || $t('entities.eventCategory.fields.example')"
          :color="hex"
        />
        <span class="category-fields__hex">{{ hex }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.category-fields {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.category-fields__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.category-fields__field label,
.category-fields__label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.category-fields__color {
  display: flex;
  align-items: center;
  gap: 12px;
}

.category-fields__hex {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-muted);
}

@media (max-width: 640px) {
  .category-fields__color {
    flex-wrap: wrap;
  }
}
</style>
