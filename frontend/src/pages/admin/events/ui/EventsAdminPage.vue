<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  AdminPageHeader,
  AppIcon,
  ColorTag,
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
  ListThumbnail,
} from '@/shared/ui'

import { useEventCategoryTypesList } from '@/entities/catalog'
import { EventFormDialog, useEventsAdmin } from '@/features/manage-events'
import type {
  CreateEventRequest,
  EventListItemResponse,
  EventResponse,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import {
  formatDate,
  formatDateTime,
  formatDateTimeRange,
  toSelectOptions,
  useCrudFeedback,
  useDeleteConfirm,
} from '@/shared/lib'

const { t } = useI18n()
const { table, create, update, remove, feature, fetchOne } = useEventsAdmin()
const categoryTypes = useEventCategoryTypesList()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const categoryOptions = computed(() => toSelectOptions(categoryTypes.data.value))

function onFeature(event: EventListItemResponse): void {
  if (!event.id || event.featured) return
  feature.mutate(event.id, {
    onSuccess: () => feedback.success(t('pages.admin.events.toasts.featured')),
    onError: (error) => feedback.error(error),
  })
}

const dialogVisible = ref(false)
const selected = ref<EventResponse | null>(null)
const loadingDetail = ref(false)
const saving = computed(() => create.isPending.value || update.isPending.value)

function openCreate(): void {
  if (loadingDetail.value) return
  selected.value = null
  dialogVisible.value = true
}

async function openEdit(event: EventListItemResponse): Promise<void> {
  if (!event.id || loadingDetail.value) return
  loadingDetail.value = true
  try {
    const detail = await fetchOne(event.id)
    if (!detail) {
      feedback.error(t('pages.admin.events.toasts.notFound'))
      return
    }
    selected.value = detail
    dialogVisible.value = true
  } catch (error) {
    feedback.error(error)
  } finally {
    loadingDetail.value = false
  }
}

function onSubmit(body: CreateEventRequest | UpdateEventRequest): void {
  if (selected.value?.id) {
    update.mutate(
      { id: selected.value.id, body: body as UpdateEventRequest },
      {
        onSuccess: () => {
          feedback.success(t('pages.admin.events.toasts.updated'))
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  create.mutate(body as CreateEventRequest, {
    onSuccess: () => {
      feedback.success(t('pages.admin.events.toasts.created'))
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(event: EventListItemResponse): void {
  requireDelete({
    header: t('pages.admin.events.delete.header'),
    message: t('pages.admin.events.delete.message', { title: event.title }),
    accept: () => {
      if (!event.id) return
      remove.mutate(event.id, {
        onSuccess: () => feedback.success(t('pages.admin.events.toasts.deleted')),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.events.header.title')"
      :subtitle="$t('pages.admin.events.header.subtitle')"
    >
      <template #actions>
        <el-button type="primary" :disabled="loadingDetail" @click="openCreate">
          <template #icon><AppIcon name="plus" /></template>
          {{ $t('pages.admin.events.newEvent') }}
        </el-button>
      </template>
    </AdminPageHeader>

    <el-table
      v-bind="table.tableProps.value"
      v-loading="table.loading.value"
      @sort-change="table.onSortChange"
    >
      <template #empty>
        <span v-if="table.isError.value">{{ $t('pages.admin.events.empty.error') }}</span>
        <span v-else>{{ $t('pages.admin.events.empty.none') }}</span>
      </template>

      <el-table-column :label="$t('common.image')" width="110">
        <template #default="{ row }">
          <ListThumbnail :thumbnail-id="row.thumbnailId" :alt="row.title" style="width: 88px" />
        </template>
      </el-table-column>

      <el-table-column prop="title" sortable="custom" min-width="190">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('title').value"
            :label="$t('pages.admin.events.columns.title')"
            :placeholder="$t('pages.admin.events.columns.searchTitle')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <span class="title-cell">
            {{ row.title }}
            <el-tag v-if="row.featured" type="warning">
              {{ $t('pages.admin.events.tag.featured') }}
            </el-tag>
          </span>
        </template>
      </el-table-column>

      <el-table-column prop="subtitle" sortable="custom" min-width="160">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('subtitle').value"
            :label="$t('pages.admin.events.columns.subtitle')"
            :placeholder="$t('pages.admin.events.columns.searchSubtitle')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ row.subtitle || '—' }}</template>
      </el-table-column>

      <el-table-column prop="categories" sortable="custom" min-width="160">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('category').value"
            :label="$t('pages.admin.events.columns.categories')"
            :options="categoryOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <div class="cats-cell">
            <ColorTag
              v-for="cat in row.categories ?? []"
              :key="cat.categoryTypeId"
              :value="cat.name ?? ''"
              :color="cat.color"
            />
            <span v-if="!row.categories?.length">—</span>
          </div>
        </template>
      </el-table-column>

      <el-table-column prop="eventStartsAt" sortable="custom" min-width="175">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('eventDate').value"
            :label="$t('pages.admin.events.columns.duration')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          {{ formatDate(row.eventStartsAt) }} – {{ formatDate(row.eventEndsAt) }}
        </template>
      </el-table-column>

      <el-table-column prop="signupStartsAt" sortable="custom" min-width="190">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('signup').value"
            :label="$t('pages.admin.events.columns.signup')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          {{ formatDateTimeRange(row.signupStartsAt, row.signupEndsAt) }}
          <small v-if="row.earlySignupStartsAt" class="signup-early">
            {{
              $t('pages.admin.events.earlySignupFrom', {
                date: formatDateTime(row.earlySignupStartsAt),
              })
            }}
          </small>
        </template>
      </el-table-column>

      <el-table-column :label="$t('common.actions')" width="150" fixed="right">
        <template #default="{ row }">
          <div class="row-actions">
            <el-button
              text
              circle
              type="primary"
              :aria-label="
                row.featured
                  ? $t('pages.admin.events.aria.featured')
                  : $t('pages.admin.events.aria.feature')
              "
              :disabled="row.featured || feature.isPending.value"
              :class="{ 'is-featured': row.featured }"
              @click="onFeature(row)"
            >
              <template #icon><AppIcon :name="row.featured ? 'star-fill' : 'star'" /></template>
            </el-button>
            <RouterLink :to="{ name: 'admin-event-detail', params: { eventId: row.id } }">
              <el-button
                text
                circle
                type="primary"
                :aria-label="$t('pages.admin.events.aria.manage')"
              >
                <template #icon><AppIcon name="cog" /></template>
              </el-button>
            </RouterLink>
            <el-button
              text
              circle
              type="primary"
              :aria-label="$t('common.edit')"
              :disabled="loadingDetail"
              @click="openEdit(row)"
            >
              <template #icon><AppIcon name="pencil" /></template>
            </el-button>
            <el-button
              text
              circle
              type="danger"
              :aria-label="$t('common.delete')"
              @click="confirmDelete(row)"
            >
              <template #icon><AppIcon name="trash" /></template>
            </el-button>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <div class="table-footer">
      <el-pagination
        v-bind="table.paginationProps.value"
        @current-change="table.onCurrentPageChange"
        @size-change="table.onPageSizeChange"
      />
    </div>

    <EventFormDialog
      v-model:visible="dialogVisible"
      :event="selected"
      :saving="saving"
      @submit="onSubmit"
    />
  </div>
</template>

<style scoped>
.table-footer {
  display: flex;
  justify-content: flex-end;
  padding: 14px 4px 0;
}

.row-actions {
  display: flex;
  align-items: center;
  gap: 2px;
}

.row-actions :deep(.el-button + .el-button) {
  margin-left: 0;
}

.title-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.cats-cell {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.is-featured :deep(.el-icon) {
  color: var(--ca-orange);
}

.signup-early {
  display: block;
  color: var(--ca-text-muted);
  font-size: 12px;
}
</style>
