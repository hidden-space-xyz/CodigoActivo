<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  AdminPageHeader,
  AppButton as Button,
  ColumnFilterDate,
  ColumnSearch,
  ListThumbnail,
  RichTextEditor,
} from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Tag from 'primevue/tag'

import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import type { ContentController, ContentItem, ContentRequest } from '../model/use-content-entity'
import { EMPTY_DOC_JSON, formatDateTime, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const props = defineProps<{
  title: string
  subtitle: string
  newLabel: string
  entityLabel: string
  controller: ContentController
}>()

const { t } = useI18n()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const table = computed(() => props.controller.table)

const dialogVisible = ref(false)
const editing = ref<ContentItem | null>(null)
const loadingDetail = ref(false)
const submitted = ref(false)
const {
  pickedFile,
  uploading,
  uploadError,
  missingThumbnail,
  reset: resetThumbnail,
  resolveThumbnailId,
} = useThumbnailUpload(() => editing.value?.thumbnailId)
const form = reactive<{ title: string; subtitle: string; description: string }>({
  title: '',
  subtitle: '',
  description: '',
})

const saving = computed(
  () => props.controller.create.isPending.value || props.controller.update.isPending.value,
)

watch([dialogVisible, editing], ([open]) => {
  if (!open) return
  submitted.value = false
  resetThumbnail()
  form.title = editing.value?.title ?? ''
  form.subtitle = editing.value?.subtitle ?? ''
  form.description = editing.value?.description ?? ''
})

function openCreate(): void {
  // Don't open a create dialog while an edit-detail fetch is in flight: the late fetch would set
  // `editing` under the already-open dialog, clobbering the form (the watch repopulates on
  // `editing` changes) and retargeting the save at that item instead of creating a new one.
  if (loadingDetail.value) return
  editing.value = null
  dialogVisible.value = true
}

async function openEdit(item: ContentItem): Promise<void> {
  if (!item.id) {
    editing.value = item
    dialogVisible.value = true
    return
  }
  // Load the full detail (which carries the description the list row omits) BEFORE opening, so the
  // form is populated once from complete data. Populating from the list row and then letting a late
  // fetch re-trigger the form watch would clobber anything the user had already typed.
  loadingDetail.value = true
  try {
    const detail = await props.controller.fetchOne(item.id)
    if (!detail) {
      // 404: the row was deleted between listing and editing — don't fall through to a create.
      feedback.error(t('widgets.contentEntityPage.toasts.notFound', { label: props.entityLabel }))
      return
    }
    editing.value = detail
    dialogVisible.value = true
  } catch (error) {
    feedback.error(error)
  } finally {
    loadingDetail.value = false
  }
}

async function save(): Promise<void> {
  submitted.value = true
  if (!form.title.trim() || !form.subtitle.trim() || missingThumbnail.value) return
  const thumbnailId = await resolveThumbnailId()
  if (!thumbnailId) return
  const body: ContentRequest = {
    title: form.title.trim(),
    subtitle: form.subtitle.trim(),
    description: form.description.trim() ? form.description : EMPTY_DOC_JSON,
    thumbnailId,
  }

  if (editing.value?.id) {
    props.controller.update.mutate(
      { id: editing.value.id, body },
      {
        onSuccess: () => {
          feedback.success(t('widgets.contentEntityPage.toasts.saved'))
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  props.controller.create.mutate(body, {
    onSuccess: () => {
      feedback.success(t('widgets.contentEntityPage.toasts.created'))
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function onFeature(item: ContentItem): void {
  if (!item.id || item.featured) return
  props.controller.feature.mutate(item.id, {
    onSuccess: () => feedback.success(t('widgets.contentEntityPage.toasts.featured')),
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(item: ContentItem): void {
  requireDelete({
    header: t('widgets.contentEntityPage.confirm.header', { label: props.entityLabel }),
    message: t('widgets.contentEntityPage.confirm.message', { title: item.title }),
    accept: () => {
      if (!item.id) return
      props.controller.remove.mutate(item.id, {
        onSuccess: () => feedback.success(t('widgets.contentEntityPage.toasts.deleted')),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader :title="title" :subtitle="subtitle">
      <template #actions>
        <Button :label="newLabel" icon="pi pi-plus" :disabled="loadingDetail" @click="openCreate" />
      </template>
    </AdminPageHeader>

    <DataTable
      lazy
      :value="table.items.value"
      :total-records="table.total.value"
      :loading="table.loading.value"
      data-key="id"
      striped-rows
      paginator
      :rows="table.rows.value"
      :first="table.first.value"
      :rows-per-page-options="[25, 50, 100]"
      :sort-field="table.sortField.value"
      :sort-order="table.sortOrder.value"
      removable-sort
      @page="table.onPage"
      @sort="table.onSort"
    >
      <template #empty>
        <span v-if="table.isError.value">{{
          $t('widgets.contentEntityPage.table.loadError')
        }}</span>
        <span v-else>{{ $t('widgets.contentEntityPage.table.empty') }}</span>
      </template>

      <Column :header="$t('common.image')" style="width: 110px">
        <template #body="{ data }">
          <ListThumbnail :thumbnail-id="data.thumbnailId" :alt="data.title" style="width: 88px" />
        </template>
      </Column>
      <Column field="title" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('title').value"
            :label="$t('widgets.contentEntityPage.columns.title')"
            :placeholder="$t('widgets.contentEntityPage.columns.searchTitle')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">
          <span class="title-cell">
            {{ data.title }}
            <Tag
              v-if="controller.canFeature && data.featured"
              :value="$t('widgets.contentEntityPage.featured')"
              severity="warn"
            />
          </span>
        </template>
      </Column>
      <Column field="subtitle" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('subtitle').value"
            :label="$t('widgets.contentEntityPage.columns.subtitle')"
            :placeholder="$t('widgets.contentEntityPage.columns.searchSubtitle')"
            @apply="table.onFilter"
          />
        </template>
      </Column>
      <Column field="createdAt" sortable style="width: 200px">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('created').value"
            :label="$t('widgets.contentEntityPage.columns.created')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ formatDateTime(data.createdAt) }}</template>
      </Column>
      <Column :header="$t('common.actions')" style="width: 170px">
        <template #body="{ data }">
          <div class="row-actions">
            <Button
              v-if="controller.canFeature"
              :icon="data.featured ? 'pi pi-star-fill' : 'pi pi-star'"
              text
              rounded
              :aria-label="
                data.featured
                  ? $t('widgets.contentEntityPage.featured')
                  : $t('widgets.contentEntityPage.feature')
              "
              :disabled="data.featured || controller.feature.isPending.value"
              :class="{ 'is-featured': data.featured }"
              @click="onFeature(data)"
            />
            <Button
              icon="pi pi-pencil"
              text
              rounded
              :aria-label="$t('common.edit')"
              :disabled="loadingDetail"
              @click="openEdit(data)"
            />
            <Button
              icon="pi pi-trash"
              text
              rounded
              severity="danger"
              :aria-label="$t('common.delete')"
              @click="confirmDelete(data)"
            />
          </div>
        </template>
      </Column>
    </DataTable>

    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="editing ? $t('widgets.contentEntityPage.dialog.editHeader', { label: entityLabel }) : newLabel"
      :style="{ width: '94vw', maxWidth: '920px' }"
      :content-style="{ maxHeight: '78vh' }"
    >
      <form class="form" @submit.prevent="save">
        <div class="form__field">
          <label>{{ $t('widgets.contentEntityPage.form.title') }}</label>
          <InputText
            v-model="form.title"
            :maxlength="200"
            :invalid="submitted && !form.title.trim()"
            fluid
          />
        </div>
        <div class="form__field">
          <label>{{ $t('widgets.contentEntityPage.form.subtitle') }}</label>
          <InputText
            v-model="form.subtitle"
            :maxlength="300"
            :invalid="submitted && !form.subtitle.trim()"
            fluid
          />
        </div>
        <div class="form__field">
          <label>{{ $t('widgets.contentEntityPage.form.description') }}</label>
          <RichTextEditor v-model="form.description" />
        </div>
        <div class="form__field">
          <label>{{ $t('common.image') }}</label>
          <ThumbnailField
            :existing-thumbnail-id="editing?.thumbnailId"
            :invalid="submitted && missingThumbnail"
            @update:file="pickedFile = $event"
          />
          <small v-if="submitted && missingThumbnail" class="form__error">{{
            $t('common.imageRequired')
          }}</small>
          <small v-if="uploadError" class="form__error">{{ uploadError }}</small>
        </div>
      </form>
      <template #footer>
        <Button
          :label="$t('common.cancel')"
          text
          severity="secondary"
          :disabled="saving || uploading"
          @click="dialogVisible = false"
        />
        <Button
          :label="$t('common.save')"
          :loading="saving || uploading || loadingDetail"
          @click="save"
        />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.row-actions {
  display: flex;
  gap: 2px;
}

.title-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.is-featured:deep(.p-button-icon) {
  color: var(--ca-orange);
}

.form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-top: 6px;
}

.form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.form__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}
</style>
