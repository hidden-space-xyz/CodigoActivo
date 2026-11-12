<script setup lang="ts">
import { watch } from 'vue'

import type { InventoryColumn, InventoryEntry, InventoryEntryInput } from '@/entities/inventory'
import {
  INVENTORY_LONG_TEXT_MAX_LENGTH,
  INVENTORY_MAX_QUANTITY,
  INVENTORY_MIN_QUANTITY,
  INVENTORY_NUMBER_MAX_LENGTH,
  INVENTORY_TEXT_MAX_LENGTH,
} from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { ColorTag } from '@/shared/ui/color-tag'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'

import { readInventoryEntryDraft, toInventoryEntryDraft } from '../model/entry-form'

const DATE_FORMAT = 'DD/MM/YYYY'
const DAY_FORMAT = 'YYYY-MM-DD'

/** Opens the dialog; each time it opens the form is refilled from `entry`. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Columns of the category, one field each. */
  columns: readonly InventoryColumn[]
  /** Entry being edited; `null` opens the dialog to add one. */
  entry: InventoryEntry | null
  /** Shows a loading state on the save button while the parent persists the entry. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid. */
  submit: [input: InventoryEntryInput]
}>()

const form = useForm({
  initial: () => toInventoryEntryDraft(props.columns, null),
  read: (draft) => readInventoryEntryDraft(props.columns, draft),
})
const { draft, errors } = form

watch(visible, (open) => {
  if (open) form.reset(toInventoryEntryDraft(props.columns, props.entry))
})

function save(): void {
  const value = form.submit()
  if (value) emit('submit', value)
}
</script>

<template>
  <FormDialog
    v-model:visible="visible"
    data-testid="inventory-entry-dialog"
    :title="
      entry
        ? $t('pages.admin.inventoryCategory.form.editHeader')
        : $t('pages.admin.inventoryCategory.form.newHeader')
    "
    width="min(560px, 92vw)"
    scroll-height="68vh"
    :saving="saving"
    @submit="save"
  >
    <FormField
      v-for="column in columns"
      :key="column.id"
      v-slot="{ control }"
      :for="`inventory-entry-${column.id}`"
      :label="column.name"
      :error="errors[column.id]"
      error-testid="inventory-entry-field-error"
      data-testid="inventory-entry-field"
    >
      <el-input-number
        v-if="column.type === 'Quantity'"
        :id="control.id"
        v-model="draft.quantity"
        :class="control.class"
        :min="INVENTORY_MIN_QUANTITY"
        :max="INVENTORY_MAX_QUANTITY"
        :precision="0"
        controls-position="right"
      />
      <el-input
        v-else-if="column.type === 'LongText'"
        v-model="draft.values[column.id]"
        v-bind="control"
        type="textarea"
        :autosize="{ minRows: 2, maxRows: 6 }"
        :maxlength="INVENTORY_LONG_TEXT_MAX_LENGTH"
      />
      <el-input
        v-else-if="column.type === 'Number'"
        v-model="draft.values[column.id]"
        v-bind="control"
        inputmode="decimal"
        :maxlength="INVENTORY_NUMBER_MAX_LENGTH"
      />
      <el-date-picker
        v-else-if="column.type === 'Date'"
        :id="control.id"
        v-model="draft.values[column.id]"
        type="date"
        :format="DATE_FORMAT"
        :value-format="DAY_FORMAT"
        :class="control.class"
      />
      <el-select
        v-else-if="column.type === 'Boolean'"
        :id="control.id"
        v-model="draft.values[column.id]"
        :class="control.class"
        clearable
      >
        <el-option
          value="true"
          data-testid="option-true"
          :label="$t('pages.admin.inventoryCategory.yes')"
        />
        <el-option
          value="false"
          data-testid="option-false"
          :label="$t('pages.admin.inventoryCategory.no')"
        />
      </el-select>
      <el-select
        v-else-if="column.type === 'List'"
        :id="control.id"
        v-model="draft.values[column.id]"
        :class="control.class"
        clearable
      >
        <el-option
          v-for="option in column.options"
          :key="option.id"
          :value="option.id"
          :label="option.name"
          :data-testid="`option-${option.id}`"
        >
          <ColorTag variant="dot" :value="option.name" :color="option.color" />
        </el-option>
      </el-select>
      <el-input
        v-else
        v-model="draft.values[column.id]"
        v-bind="control"
        :maxlength="INVENTORY_TEXT_MAX_LENGTH"
      />
    </FormField>
  </FormDialog>
</template>
