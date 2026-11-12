<script setup lang="ts">
import { CATALOG_NAME_MAX_LENGTH } from '@/shared/config'
import { ColorField } from '@/shared/ui/color-field'
import { FormField } from '@/shared/ui/form-field'

/** Name of the category; the preview tag shows it. */
const name = defineModel<string>('name', { required: true })

/** Color of the category, with or without its leading `#`. */
const color = defineModel<string>('color', { required: true })

defineProps<{
  /** Marks the name as refused. */
  invalid: boolean
}>()
</script>

<template>
  <div class="category-fields">
    <FormField
      v-slot="{ control }"
      for="event-category-name"
      :label="$t('common.name')"
      :invalid="invalid"
    >
      <el-input
        v-model="name"
        v-bind="control"
        data-testid="event-category-fields-name"
        :maxlength="CATALOG_NAME_MAX_LENGTH"
      />
    </FormField>
    <ColorField
      v-model="color"
      label-id="event-category-color-label"
      :label="$t('entities.eventCategory.fields.color')"
      :preview="name.trim() || $t('entities.eventCategory.fields.example')"
    />
  </div>
</template>

<style scoped>
.category-fields {
  display: flex;
  flex-direction: column;
  gap: 14px;
}
</style>
