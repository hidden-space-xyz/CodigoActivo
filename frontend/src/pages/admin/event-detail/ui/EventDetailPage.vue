<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import {
  AdminPageHeader,
  AppButton as Button,
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
  ListThumbnail,
} from '@/shared/ui'

import { ActivityFormDialog, useActivities } from '@/features/manage-activities'
import { useActivityModalityTypesList, useActivityRoleTypesList } from '@/entities/catalog'
import { useEvent, useEventSummary } from '@/features/manage-events'
import EventAttendeesTab from './EventAttendeesTab.vue'
import EventOpinionsTab from './EventOpinionsTab.vue'
import type {
  ActivityResponse,
  CreateActivityRequest,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import {
  formatDateTimeRange,
  formatNumber,
  toSelectOptions,
  useCrudFeedback,
  useDeleteConfirm,
} from '@/shared/lib'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const eventId = computed(() => String(route.params.eventId))

const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const activeTab = ref<string | number>('activities')

const event = useEvent(eventId)
const summary = useEventSummary(eventId)
const activities = useActivities(eventId)
const modalityTypes = useActivityModalityTypesList()
const roleTypes = useActivityRoleTypesList()

const summaryCards = computed(() => {
  const data = summary.data.value
  const cards = [
    { label: t('pages.admin.eventDetail.tabs.activities'), value: data?.activitiesCount ?? 0 },
  ]
  for (const role of data?.roleTypeBreakdown ?? []) {
    cards.push({ label: role.roleTypeName ?? '—', value: role.approvedAssignments ?? 0 })
  }
  return cards
})

const ratingsCount = computed(() => summary.data.value?.ratingsCount ?? 0)

const ratingsAverage = computed(() => {
  const average = summary.data.value?.ratingsAverage
  return average == null ? '—' : formatNumber(Number(average.toFixed(1)))
})

const modalityOptions = computed(() => toSelectOptions(modalityTypes.data.value))

function onModalityFilter(value: string | boolean | null): void {
  activities.modalityTypeId.value = typeof value === 'string' ? value : null
}

const activityDialogVisible = ref(false)
const selectedActivity = ref<ActivityResponse | null>(null)
const activitySaving = computed(
  () => activities.create.isPending.value || activities.update.isPending.value,
)

function openCreateActivity(): void {
  selectedActivity.value = null
  activityDialogVisible.value = true
}

function openBadges(): void {
  void router.push({ name: 'admin-event-badges', params: { eventId: eventId.value } })
}

function openRoster(): void {
  void router.push({ name: 'admin-event-roster', params: { eventId: eventId.value } })
}

async function openEditActivity(activity: ActivityResponse): Promise<void> {
  selectedActivity.value = activity
  activityDialogVisible.value = true
  if (!activity.id) return
  try {
    const fresh = await activities.fetchOne(activity.id)
    if (fresh) selectedActivity.value = fresh
  } catch {}
}

function onActivitySubmit(body: CreateActivityRequest | UpdateActivityRequest): void {
  if (selectedActivity.value?.id) {
    activities.update.mutate(
      { id: selectedActivity.value.id, body: body as UpdateActivityRequest },
      {
        onSuccess: () => {
          feedback.success(t('pages.admin.eventDetail.toast.activityUpdated'))
          activityDialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  activities.create.mutate(body as CreateActivityRequest, {
    onSuccess: () => {
      feedback.success(t('pages.admin.eventDetail.toast.activityCreated'))
      activityDialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDeleteActivity(activity: ActivityResponse): void {
  requireDelete({
    header: t('pages.admin.eventDetail.deleteConfirm.header'),
    message: t('pages.admin.eventDetail.deleteConfirm.message', { title: activity.title }),
    accept: () => {
      if (!activity.id) return
      activities.remove.mutate(activity.id, {
        onSuccess: () => feedback.success(t('pages.admin.eventDetail.toast.activityDeleted')),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <RouterLink :to="{ name: 'admin-events' }" class="back">{{
      $t('pages.admin.eventDetail.back')
    }}</RouterLink>

    <AdminPageHeader
      :title="event.data.value?.title ?? $t('pages.admin.eventDetail.headerFallback')"
      :subtitle="event.data.value?.subtitle ?? ''"
    >
      <template #actions>
        <Button
          :label="$t('pages.admin.eventDetail.newActivity')"
          icon="plus"
          @click="openCreateActivity"
        />
        <Button
          :label="$t('pages.admin.eventDetail.printBadges')"
          icon="print"
          @click="openBadges"
        />
        <Button
          :label="$t('pages.admin.eventDetail.printRoster')"
          icon="list"
          @click="openRoster"
        />
      </template>
    </AdminPageHeader>

    <div class="summary">
      <div v-for="card in summaryCards" :key="card.label" class="summary__card">
        <div class="summary__value">{{ card.value }}</div>
        <div class="summary__label">{{ card.label }}</div>
      </div>
      <div class="summary__card">
        <div class="summary__value">
          {{ ratingsAverage }}
          <span v-if="ratingsCount > 0" class="summary__unit">/5</span>
        </div>
        <div class="summary__label">
          {{ $t('pages.admin.eventDetail.summary.ratings', ratingsCount) }}
        </div>
      </div>
    </div>

    <el-tabs v-model="activeTab" class="tabs">
      <el-tab-pane :label="$t('pages.admin.eventDetail.tabs.activities')" name="activities">
        <el-table
          v-bind="activities.table.tableProps.value"
          v-loading="activities.table.loading.value"
          @sort-change="activities.table.onSortChange"
        >
          <template #empty>
            <span v-if="activities.table.isError.value">
              {{ $t('pages.admin.eventDetail.empty.error') }}
            </span>
            <span v-else>{{ $t('pages.admin.eventDetail.empty.none') }}</span>
          </template>

          <el-table-column :label="$t('common.image')" width="110">
            <template #default="{ row }">
              <ListThumbnail :thumbnail-id="row.thumbnailId" :alt="row.title" style="width: 88px" />
            </template>
          </el-table-column>
          <el-table-column prop="title" min-width="200" sortable="custom">
            <template #header>
              <ColumnSearch
                v-model="activities.table.columnFilter('title').value"
                :label="$t('pages.admin.eventDetail.columns.title')"
                :placeholder="$t('pages.admin.eventDetail.columns.searchTitle')"
                @apply="activities.table.onFilter"
              />
            </template>
          </el-table-column>
          <el-table-column prop="activityStartsAt" min-width="210" sortable="custom">
            <template #header>
              <ColumnFilterDate
                v-model="activities.table.columnFilter('activityDate').value"
                :label="$t('pages.admin.eventDetail.columns.schedule')"
                @apply="activities.table.onFilter"
              />
            </template>
            <template #default="{ row }">
              {{ formatDateTimeRange(row.activityStartsAt, row.activityEndsAt) }}
            </template>
          </el-table-column>
          <el-table-column prop="modalityName" min-width="180" sortable="custom">
            <template #header>
              <ColumnFilterSelect
                :model-value="activities.modalityTypeId.value"
                :label="$t('pages.admin.eventDetail.columns.modality')"
                :options="modalityOptions"
                @update:model-value="onModalityFilter"
              />
            </template>
            <template #default="{ row }">
              <div class="modality-cell">
                <span class="modality-cell__type">{{ row.modalityName || '—' }}</span>
                <span v-if="row.location" class="modality-cell__loc">{{ row.location }}</span>
              </div>
            </template>
          </el-table-column>
          <el-table-column :label="$t('common.actions')" width="120" fixed="right">
            <template #default="{ row }">
              <div class="row-actions">
                <Button
                  icon="pencil"
                  text
                  circle
                  :aria-label="$t('common.edit')"
                  @click="openEditActivity(row)"
                />
                <Button
                  icon="trash"
                  text
                  circle
                  type="danger"
                  :aria-label="$t('common.delete')"
                  @click="confirmDeleteActivity(row)"
                />
              </div>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination
          v-bind="activities.table.paginationProps.value"
          class="paginator"
          @update:current-page="activities.table.onCurrentPageChange"
          @update:page-size="activities.table.onPageSizeChange"
        />
      </el-tab-pane>
      <el-tab-pane :label="$t('pages.admin.eventDetail.tabs.attendees')" name="attendees">
        <EventAttendeesTab
          :event-id="eventId"
          :active="activeTab === 'attendees'"
          :activities="activities.options.data.value ?? []"
          :activities-loading="activities.options.isLoading.value"
          :activities-error="activities.options.isError.value"
        />
      </el-tab-pane>
      <el-tab-pane :label="$t('pages.admin.eventDetail.tabs.opinions')" name="opinions">
        <EventOpinionsTab :event-id="eventId" :active="activeTab === 'opinions'" />
      </el-tab-pane>
    </el-tabs>

    <ActivityFormDialog
      v-model:visible="activityDialogVisible"
      :activity="selectedActivity"
      :modality-types="modalityTypes.data.value ?? []"
      :role-types="roleTypes.data.value ?? []"
      :saving="activitySaving"
      :event-start="event.data.value?.eventStartsAt ?? null"
      :event-end="event.data.value?.eventEndsAt ?? null"
      @submit="onActivitySubmit"
    />
  </div>
</template>

<style scoped>
.back {
  display: inline-flex;
  align-items: center;
  min-height: var(--ca-tap);
  margin-bottom: 4px;
  color: var(--ca-text-muted);
  text-decoration: none;
  font-size: 14px;
}

.back:hover {
  color: var(--ca-text);
}

.summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(150px, 100%), 1fr));
  gap: 12px;
  margin-bottom: 26px;
}

.summary__card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 16px;
}

.summary__value {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 26px;
  color: var(--ca-text-bright);
}

.summary__unit {
  font-size: 16px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.summary__label {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.tabs {
  margin-bottom: 30px;
}

.paginator {
  margin-top: 14px;
  justify-content: flex-end;
}

.row-actions {
  display: flex;
  gap: 2px;
}

.modality-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.modality-cell__type {
  font-weight: 600;
}

.modality-cell__loc {
  font-size: 12.5px;
  color: var(--ca-text-muted);
}
</style>
