<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { AppButton as Button, ColorTag, ColumnSearch } from '@/shared/ui'

import { useEventCategories } from '../model/categories'
import type { EventCategoryTypeResponse } from '@/shared/api/generated/models'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const DEFAULT_CATEGORY_COLOR = '#6366F1'

const { t } = useI18n()
const { table, create, update, remove } = useEventCategories()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const dialogVisible = ref(false)
const selected = ref<EventCategoryTypeResponse | null>(null)
const submitted = ref(false)
const form = reactive<{ name: string; color: string }>({
  name: '',
  color: DEFAULT_CATEGORY_COLOR,
})

const saving = computed(() => create.isPending.value || update.isPending.value)
const colorHex = computed(() => `#${form.color.replace(/^#/, '')}`)

function setFormColor(value: string | null): void {
  form.color = value ?? DEFAULT_CATEGORY_COLOR
}

watch(dialogVisible, (open) => {
  if (!open) return
  submitted.value = false
  form.name = selected.value?.name ?? ''
  form.color = selected.value?.color ?? DEFAULT_CATEGORY_COLOR
})

function openCreate(): void {
  selected.value = null
  dialogVisible.value = true
}

function openEdit(item: EventCategoryTypeResponse): void {
  selected.value = item
  dialogVisible.value = true
}

function save(): void {
  submitted.value = true
  if (!form.name.trim()) return
  const body = { name: form.name.trim(), color: colorHex.value }
  if (selected.value?.id) {
    update.mutate(
      { id: selected.value.id, body },
      {
        onSuccess: () => {
          feedback.success(t('features.manageCatalogs.updated'))
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  create.mutate(body, {
    onSuccess: () => {
      feedback.success(t('features.manageCatalogs.created'))
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(item: EventCategoryTypeResponse): void {
  requireDelete({
    header: t('features.manageCatalogs.deleteHeader'),
    message: t('features.manageCatalogs.deleteMessage', { name: item.name }),
    accept: () => {
      if (!item.id) return
      remove.mutate(item.id, {
        onSuccess: () => feedback.success(t('features.manageCatalogs.deleted')),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <section class="catalog">
    <div class="catalog__head">
      <h2 class="catalog__title">{{ $t('features.manageCatalogs.title') }}</h2>
      <Button
        :label="$t('features.manageCatalogs.newButton')"
        icon="plus"
        type="primary"
        size="small"
        @click="openCreate"
      />
    </div>

    <el-table
      v-bind="table.tableProps.value"
      v-loading="table.loading.value"
      @sort-change="table.onSortChange"
    >
      <template #empty>
        <span v-if="table.isError.value">{{ $t('features.manageCatalogs.loadError') }}</span>
        <span v-else>{{ $t('features.manageCatalogs.empty') }}</span>
      </template>

      <el-table-column prop="name" sortable="custom">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            :label="$t('common.name')"
            :placeholder="$t('features.manageCatalogs.searchName')"
            @apply="table.onFilter"
          />
        </template>
      </el-table-column>
      <el-table-column prop="color" sortable="custom" width="160">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('color').value"
            :label="$t('features.manageCatalogs.color')"
            :placeholder="$t('features.manageCatalogs.searchColor')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ColorTag :value="row.name ?? ''" :color="row.color" />
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="120">
        <template #default="{ row }">
          <div class="catalog__actions">
            <Button
              icon="pencil"
              text
              circle
              :aria-label="$t('common.edit')"
              @click="openEdit(row)"
            />
            <Button
              icon="trash"
              text
              circle
              type="danger"
              :aria-label="$t('common.delete')"
              @click="confirmDelete(row)"
            />
          </div>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-bind="table.paginationProps.value"
      class="table-pagination"
      @current-change="table.onCurrentPageChange"
      @size-change="table.onPageSizeChange"
    />

    <el-dialog
      v-model="dialogVisible"
      :title="
        selected
          ? $t('features.manageCatalogs.editHeader')
          : $t('features.manageCatalogs.newHeader')
      "
      width="min(440px, 92vw)"
      append-to-body
    >
      <form class="catalog__form" @submit.prevent="save">
        <div class="catalog__field">
          <label>{{ $t('common.name') }}</label>
          <el-input
            v-model="form.name"
            :maxlength="120"
            :class="{ 'ca-invalid': submitted && !form.name.trim() }"
          />
        </div>
        <div class="catalog__field">
          <label>{{ $t('features.manageCatalogs.color') }}</label>
          <div class="catalog__color">
            <el-color-picker
              :model-value="colorHex"
              color-format="hex"
              @update:model-value="setFormColor"
            />
            <ColorTag
              :value="form.name.trim() || $t('features.manageCatalogs.example')"
              :color="colorHex"
            />
            <span class="catalog__hex">{{ colorHex }}</span>
          </div>
        </div>
      </form>
      <template #footer>
        <Button
          :label="$t('common.cancel')"
          text
          :disabled="saving"
          @click="dialogVisible = false"
        />
        <Button :label="$t('common.save')" type="primary" :loading="saving" @click="save" />
      </template>
    </el-dialog>
  </section>
</template>

<style scoped>
.catalog {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 14px;
  padding: 18px 20px;
}

.catalog__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
}

.catalog__title {
  font-family: var(--ca-font-display);
  font-size: 18px;
  font-weight: 600;
  color: var(--ca-text-bright);
}

.catalog__actions {
  display: flex;
  gap: 2px;
}

.table-pagination {
  margin-top: 14px;
  justify-content: flex-end;
}

.catalog__form {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding-top: 6px;
}

.catalog__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.catalog__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.catalog__color {
  display: flex;
  align-items: center;
  gap: 12px;
}

.catalog__hex {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-muted);
}

.catalog__form :deep(.ca-invalid) {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}
</style>
