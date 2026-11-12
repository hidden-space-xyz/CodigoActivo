<script setup lang="ts">
import { watch } from 'vue'
import { useI18n } from 'vue-i18n'

import type { InventoryCategoryInput } from '@/entities/inventory'
import {
  INVENTORY_CATEGORY_NAME_MAX_LENGTH,
  INVENTORY_COLUMN_NAME_MAX_LENGTH,
  INVENTORY_MAX_COLUMNS,
  INVENTORY_MAX_OPTIONS,
} from '@/shared/config'
import { useDeleteConfirm } from '@/shared/lib/feedback'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'
import { ColorField } from '@/shared/ui/color-field'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'

import {
  columnTypeLabelKey,
  draftColumnUsage,
  INVENTORY_COLUMN_TYPES,
  moveItem,
  newColumnDraft,
  newOptionDraft,
  quantityTakenElsewhere,
  readInventoryCategoryDraft,
  toInventoryCategoryDraft,
  type EditedInventoryCategory,
  type InventoryColumnDraft,
} from '../model/category-form'

/** Opens the dialog; each time it opens the form is refilled from `category`. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Category being edited with what its entries say; `null` opens the dialog in create mode. */
  category: EditedInventoryCategory | null
  /** Shows a loading state on the save button while the parent persists the category. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid. */
  submit: [input: InventoryCategoryInput]
}>()

const { t } = useI18n()
const { confirmDelete } = useDeleteConfirm()
const form = useForm({
  initial: () => toInventoryCategoryDraft(null),
  read: readInventoryCategoryDraft,
})
const { draft, errors } = form

watch(visible, (open) => {
  if (open) form.reset(toInventoryCategoryDraft(props.category))
})

function usage(column: InventoryColumnDraft) {
  return draftColumnUsage(column, props.category)
}

function changeType(column: InventoryColumnDraft, type: InventoryColumnDraft['type']): void {
  column.type = type
  if (type === 'List' && column.options.length === 0) column.options.push(newOptionDraft())
}

function removeColumn(index: number): void {
  const column = draft.columns[index]
  if (!column) return
  const filled = usage(column).filled
  if (filled === 0) {
    draft.columns.splice(index, 1)
    return
  }
  confirmDelete({
    header: t('pages.admin.inventory.form.removeColumnConfirm.header'),
    message: t(
      'pages.admin.inventory.form.removeColumnConfirm.message',
      { name: column.name, count: filled },
      filled,
    ),
    accept: () => {
      draft.columns.splice(draft.columns.indexOf(column), 1)
    },
  })
}

function save(): void {
  const value = form.submit()
  if (value) emit('submit', value)
}
</script>

<template>
  <FormDialog
    v-model:visible="visible"
    data-testid="inventory-category-dialog"
    :title="
      category
        ? $t('pages.admin.inventory.form.editHeader')
        : $t('pages.admin.inventory.form.newHeader')
    "
    width="min(760px, 94vw)"
    scroll-height="68vh"
    :saving="saving"
    @submit="save"
  >
    <FormField
      v-slot="{ control }"
      for="inventory-category-name"
      :label="$t('common.name')"
      :error="errors.name"
      error-testid="inventory-category-name-error"
    >
      <el-input
        v-model="draft.name"
        v-bind="control"
        data-testid="inventory-category-name"
        :maxlength="INVENTORY_CATEGORY_NAME_MAX_LENGTH"
      />
    </FormField>

    <div class="ca-field">
      <div id="inventory-columns-label" class="ca-field__label">
        {{ $t('pages.admin.inventory.form.columns') }}
      </div>
      <p class="ca-field__hint">{{ $t('pages.admin.inventory.form.columnsHint') }}</p>
      <ol class="columns" aria-labelledby="inventory-columns-label">
        <li
          v-for="(column, index) in draft.columns"
          :key="column.key"
          class="column"
          data-testid="inventory-column"
        >
          <div class="column__head">
            <FormField
              v-slot="{ control }"
              :for="`inventory-column-${column.key}-name`"
              :label="$t('pages.admin.inventory.form.columnName')"
              :error="errors[`${column.key}.name`]"
              error-testid="inventory-column-name-error"
            >
              <el-input
                v-model="column.name"
                v-bind="control"
                data-testid="inventory-column-name"
                :maxlength="INVENTORY_COLUMN_NAME_MAX_LENGTH"
              />
            </FormField>
            <FormField
              v-slot="{ control }"
              :for="`inventory-column-${column.key}-type`"
              :label="$t('pages.admin.inventory.form.columnType')"
              :error="errors[`${column.key}.type`]"
              :hint="
                usage(column).filled > 0 ? $t('pages.admin.inventory.form.typeLocked') : undefined
              "
            >
              <el-select
                :id="control.id"
                :model-value="column.type"
                :class="control.class"
                data-testid="inventory-column-type"
                :disabled="usage(column).filled > 0"
                @update:model-value="changeType(column, $event)"
              >
                <el-option
                  v-for="type in INVENTORY_COLUMN_TYPES"
                  :key="type"
                  :value="type"
                  :label="$t(columnTypeLabelKey(type))"
                  :disabled="type === 'Quantity' && quantityTakenElsewhere(draft, column)"
                  :data-testid="`option-${type}`"
                />
              </el-select>
            </FormField>
            <div class="column__actions">
              <ActionButton
                icon="chevron-up"
                circle
                :aria-label="$t('pages.admin.inventory.form.moveUp')"
                :disabled="index === 0"
                @click="moveItem(draft.columns, index, -1)"
              />
              <ActionButton
                icon="chevron-down"
                circle
                :aria-label="$t('pages.admin.inventory.form.moveDown')"
                :disabled="index === draft.columns.length - 1"
                @click="moveItem(draft.columns, index, 1)"
              />
              <ActionButton
                icon="trash"
                circle
                data-testid="inventory-column-remove"
                :aria-label="$t('pages.admin.inventory.form.removeColumn')"
                :disabled="draft.columns.length === 1"
                @click="removeColumn(index)"
              />
            </div>
          </div>

          <el-checkbox
            v-model="column.required"
            data-testid="inventory-column-required"
            class="ca-checkbox-text"
            :disabled="!column.required && usage(column).empty"
          >
            {{ $t('pages.admin.inventory.form.required') }}
          </el-checkbox>
          <p v-if="!column.required && usage(column).empty" class="ca-field__hint">
            {{ $t('pages.admin.inventory.form.requiredLocked') }}
          </p>

          <div v-if="column.type === 'List'" class="column__options">
            <div :id="`inventory-column-${column.key}-options`" class="ca-field__label">
              {{ $t('pages.admin.inventory.form.options') }}
            </div>
            <p v-if="errors[`${column.key}.options`]" class="ca-field__error">
              {{ errors[`${column.key}.options`] }}
            </p>
            <ol class="options" :aria-labelledby="`inventory-column-${column.key}-options`">
              <li
                v-for="(option, optionIndex) in column.options"
                :key="option.key"
                class="option"
                data-testid="inventory-option"
              >
                <FormField
                  v-slot="{ control }"
                  :for="`inventory-option-${option.key}-name`"
                  :label="$t('pages.admin.inventory.form.optionName')"
                  :error="errors[`${option.key}.name`]"
                  error-testid="inventory-option-name-error"
                >
                  <el-input
                    v-model="option.name"
                    v-bind="control"
                    data-testid="inventory-option-name"
                    :maxlength="INVENTORY_COLUMN_NAME_MAX_LENGTH"
                  />
                </FormField>
                <ColorField
                  v-model="option.color"
                  :label-id="`inventory-option-${option.key}-color`"
                  :label="$t('pages.admin.inventory.form.optionColor')"
                  :preview="option.name.trim() || $t('pages.admin.inventory.form.optionName')"
                />
                <div class="option__actions">
                  <ActionButton
                    icon="chevron-up"
                    circle
                    :aria-label="$t('pages.admin.inventory.form.moveUp')"
                    :disabled="optionIndex === 0"
                    @click="moveItem(column.options, optionIndex, -1)"
                  />
                  <ActionButton
                    icon="chevron-down"
                    circle
                    :aria-label="$t('pages.admin.inventory.form.moveDown')"
                    :disabled="optionIndex === column.options.length - 1"
                    @click="moveItem(column.options, optionIndex, 1)"
                  />
                  <ActionButton
                    icon="trash"
                    circle
                    data-testid="inventory-option-remove"
                    :aria-label="$t('pages.admin.inventory.form.removeOption')"
                    :tooltip="
                      option.id && usage(column).chosen.has(option.id)
                        ? $t('pages.admin.inventory.form.optionInUse')
                        : $t('pages.admin.inventory.form.removeOption')
                    "
                    :disabled="!!option.id && usage(column).chosen.has(option.id)"
                    @click="column.options.splice(optionIndex, 1)"
                  />
                </div>
              </li>
            </ol>
            <ActionButton
              icon="plus"
              data-testid="inventory-option-add"
              :label="$t('pages.admin.inventory.form.addOption')"
              :disabled="column.options.length >= INVENTORY_MAX_OPTIONS"
              @click="column.options.push(newOptionDraft())"
            />
          </div>
        </li>
      </ol>
      <ActionButton
        icon="plus"
        data-testid="inventory-column-add"
        :label="$t('pages.admin.inventory.form.addColumn')"
        :disabled="draft.columns.length >= INVENTORY_MAX_COLUMNS"
        @click="draft.columns.push(newColumnDraft())"
      />
    </div>
  </FormDialog>
</template>

<style scoped>
.columns,
.options {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin: 0 0 12px;
  padding: 0;
  list-style: none;
}

.column {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 14px;
  border: 1px solid var(--ca-border);
  border-radius: var(--ca-radius-md);
  background: var(--ca-surface);
}

.column__head {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(0, 1fr) auto;
  gap: 12px;
  align-items: start;
}

.column__actions,
.option__actions {
  display: flex;
  gap: 4px;
  padding-top: 22px;
}

.column__options {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--ca-border-soft);
}

.option {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) auto;
  gap: 12px;
  align-items: start;
}

@media (max-width: 640px) {
  .column__head,
  .option {
    grid-template-columns: 1fr;
  }

  .column__actions,
  .option__actions {
    padding-top: 0;
  }
}
</style>
