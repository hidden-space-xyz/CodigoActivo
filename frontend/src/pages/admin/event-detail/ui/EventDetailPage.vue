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
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'

import { ActivityFormDialog, useActivities } from '@/features/manage-activities'
import { useActivityModalityTypesList, useActivityRoleTypesList } from '@/entities/catalog'
import { useEvent, useEventSummary } from '@/features/manage-events'
import EventAttendeesTab from './EventAttendeesTab.vue'
import type {
  ActivityResponse,
  CreateActivityRequest,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import { formatDateTimeRange, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

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

const modalityOptions = computed(() =>
  (modalityTypes.data.value ?? []).map((modality) => ({
    label: modality.name ?? '—',
    value: modality.id ?? '',
  })),
)

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
          icon="pi pi-plus"
          severity="secondary"
          @click="openCreateActivity"
        />
        <Button
          :label="$t('pages.admin.eventDetail.printBadges')"
          icon="pi pi-print"
          severity="secondary"
          @click="openBadges"
        />
        <Button
          :label="$t('pages.admin.eventDetail.printRoster')"
          icon="pi pi-list"
          severity="secondary"
          @click="openRoster"
        />
      </template>
    </AdminPageHeader>

    <div class="summary">
      <div v-for="card in summaryCards" :key="card.label" class="summary__card">
        <div class="summary__value">{{ card.value }}</div>
        <div class="summary__label">{{ card.label }}</div>
      </div>
    </div>

    <Tabs v-model:value="activeTab" class="tabs">
      <TabList>
        <Tab value="activities">{{ $t('pages.admin.eventDetail.tabs.activities') }}</Tab>
        <Tab value="attendees">{{ $t('pages.admin.eventDetail.tabs.attendees') }}</Tab>
      </TabList>
      <TabPanels>
        <TabPanel value="activities">
          <DataTable
            lazy
            :value="activities.table.items.value"
            :total-records="activities.table.total.value"
            :loading="activities.table.loading.value"
            data-key="id"
            striped-rows
            paginator
            :rows="activities.table.rows.value"
            :first="activities.table.first.value"
            :rows-per-page-options="[25, 50, 100]"
            :sort-field="activities.table.sortField.value"
            :sort-order="activities.table.sortOrder.value"
            removable-sort
            @page="activities.table.onPage"
            @sort="activities.table.onSort"
          >
            <template #empty>
              <span v-if="activities.table.isError.value">
                {{ $t('pages.admin.eventDetail.empty.error') }}
              </span>
              <span v-else>{{ $t('pages.admin.eventDetail.empty.none') }}</span>
            </template>

            <Column :header="$t('common.image')" style="width: 110px">
              <template #body="{ data }">
                <ListThumbnail
                  :thumbnail-id="data.thumbnailId"
                  :alt="data.title"
                  style="width: 88px"
                />
              </template>
            </Column>
            <Column field="title" sortable>
              <template #header>
                <ColumnSearch
                  v-model="activities.table.columnFilter('title').value"
                  :label="$t('pages.admin.eventDetail.columns.title')"
                  :placeholder="$t('pages.admin.eventDetail.columns.searchTitle')"
                  @apply="activities.table.onFilter"
                />
              </template>
            </Column>
            <Column field="activityStartsAt" sortable>
              <template #header>
                <ColumnFilterDate
                  v-model="activities.table.columnFilter('activityDate').value"
                  :label="$t('pages.admin.eventDetail.columns.schedule')"
                  @apply="activities.table.onFilter"
                />
              </template>
              <template #body="{ data }">
                {{ formatDateTimeRange(data.activityStartsAt, data.activityEndsAt) }}
              </template>
            </Column>
            <Column field="modalityName" sortable>
              <template #header>
                <ColumnFilterSelect
                  :model-value="activities.modalityTypeId.value"
                  :label="$t('pages.admin.eventDetail.columns.modality')"
                  :options="modalityOptions"
                  @update:model-value="onModalityFilter"
                />
              </template>
              <template #body="{ data }">
                <div class="modality-cell">
                  <span class="modality-cell__type">{{ data.modalityName || '—' }}</span>
                  <span v-if="data.location" class="modality-cell__loc">{{ data.location }}</span>
                </div>
              </template>
            </Column>
            <Column :header="$t('common.actions')" style="width: 120px">
              <template #body="{ data }">
                <div class="row-actions">
                  <Button
                    icon="pi pi-pencil"
                    text
                    rounded
                    :aria-label="$t('common.edit')"
                    @click="openEditActivity(data)"
                  />
                  <Button
                    icon="pi pi-trash"
                    text
                    rounded
                    severity="danger"
                    :aria-label="$t('common.delete')"
                    @click="confirmDeleteActivity(data)"
                  />
                </div>
              </template>
            </Column>
          </DataTable>
        </TabPanel>
        <TabPanel value="attendees">
          <EventAttendeesTab
            :event-id="eventId"
            :active="activeTab === 'attendees'"
            :activities="activities.options.data.value ?? []"
            :activities-loading="activities.options.isLoading.value"
            :activities-error="activities.options.isError.value"
          />
        </TabPanel>
      </TabPanels>
    </Tabs>

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
  display: inline-block;
  margin-bottom: 14px;
  color: var(--ca-text-muted);
  text-decoration: none;
  font-size: 14px;
}

.back:hover {
  color: var(--ca-text);
}

.summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
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

.summary__label {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.tabs {
  margin-bottom: 30px;
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
