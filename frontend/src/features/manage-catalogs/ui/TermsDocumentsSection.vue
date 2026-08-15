<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { AppButton as Button, ColumnSearch, RichTextEditor } from '@/shared/ui'

import { uploadFileRequest } from '@/entities/file'
import { useTermsDocuments } from '../model/useTermsDocuments'
import type { TermsDocumentResponse } from '@/shared/api/generated/models'
import { isRichTextBlank, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const { t } = useI18n()
const { table, create, update, remove } = useTermsDocuments()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const dialogVisible = ref(false)
const selected = ref<TermsDocumentResponse | null>(null)
const submitted = ref(false)
const form = reactive<{ name: string; description: string }>({
  name: '',
  description: '',
})

const saving = computed(() => create.isPending.value || update.isPending.value)
const descriptionMissing = computed(() => isRichTextBlank(form.description))

watch(dialogVisible, (open) => {
  if (!open) return
  submitted.value = false
  form.name = selected.value?.name ?? ''
  form.description = selected.value?.description ?? ''
})

function openCreate(): void {
  selected.value = null
  dialogVisible.value = true
}

function openEdit(item: TermsDocumentResponse): void {
  selected.value = item
  dialogVisible.value = true
}

function save(): void {
  submitted.value = true
  if (!form.name.trim() || descriptionMissing.value) return
  const body = { name: form.name.trim(), description: form.description }
  if (selected.value?.id) {
    update.mutate(
      { id: selected.value.id, body },
      {
        onSuccess: () => {
          feedback.success(t('features.manageCatalogs.terms.updated'))
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  create.mutate(body, {
    onSuccess: () => {
      feedback.success(t('features.manageCatalogs.terms.created'))
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(item: TermsDocumentResponse): void {
  requireDelete({
    header: t('features.manageCatalogs.terms.deleteHeader'),
    message: t('features.manageCatalogs.terms.deleteMessage', { name: item.name }),
    accept: () => {
      if (!item.id) return
      remove.mutate(item.id, {
        onSuccess: () => feedback.success(t('features.manageCatalogs.terms.deleted')),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <section class="catalog">
    <div class="catalog__head">
      <h2 class="catalog__title">{{ $t('features.manageCatalogs.terms.title') }}</h2>
      <Button
        :label="$t('features.manageCatalogs.terms.newButton')"
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
        <span v-if="table.isError.value">{{ $t('features.manageCatalogs.terms.loadError') }}</span>
        <span v-else>{{ $t('features.manageCatalogs.terms.empty') }}</span>
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
      <el-table-column :label="$t('common.actions')" width="120" align="center">
        <template #default="{ row }">
          <div class="ca-row-actions">
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
          ? $t('features.manageCatalogs.terms.editHeader')
          : $t('features.manageCatalogs.terms.newHeader')
      "
      width="min(860px, 94vw)"
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
          <label>{{ $t('features.manageCatalogs.terms.content') }}</label>
          <RichTextEditor v-model="form.description" :upload="uploadFileRequest" />
          <small v-if="submitted && descriptionMissing" class="catalog__error">{{
            $t('features.manageCatalogs.terms.contentRequired')
          }}</small>
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

.table-pagination {
  margin-top: 14px;
  justify-content: flex-end;
}

.catalog__form {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding-top: 6px;
  max-height: 68vh;
  overflow-y: auto;
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

.catalog__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}

.catalog__form :deep(.ca-invalid) {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}

@media (max-width: 640px) {
  .catalog__form {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
