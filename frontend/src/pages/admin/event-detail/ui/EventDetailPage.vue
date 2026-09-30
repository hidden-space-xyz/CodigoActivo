<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import { activityQueries } from '@/entities/activity'
import { eventQueries } from '@/entities/event'
import { formatDateTimeRange } from '@/shared/lib/date'
import { formatNumber } from '@/shared/lib/number'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { BrandButton } from '@/shared/ui/brand-button'
import {
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
  toSelectOptions,
} from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'

import { eventReportQueries } from '../api/queries'
import { useEventActivitiesAdmin } from '../model/use-event-activities-admin'
import ActivityFormDialog from './ActivityFormDialog.vue'
import EventAttendeesTab from './EventAttendeesTab.vue'
import EventOpinionsTab from './EventOpinionsTab.vue'

const props = defineProps<{
  /** Event id from the `/admin/events/:eventId` route param. */
  eventId: string
}>()

const { t } = useI18n()
const router = useRouter()
const activeTab = ref<string | number>('activities')

const event = useQuery(() => eventQueries.detail(props.eventId))
const stats = useQuery(() => eventReportQueries.stats(props.eventId))
const modalities = useQuery(activityQueries.modalities())
const roles = useQuery(activityQueries.roles())
const { table, modalityId, dialog, saving, save, confirmRemove } = useEventActivitiesAdmin(
  () => props.eventId,
)
const { visible, editing, loading, openCreate, openEdit } = dialog

const summaryCards = computed(() => [
  {
    label: t('pages.admin.eventDetail.tabs.activities'),
    value: stats.data.value?.activitiesCount ?? 0,
  },
  ...(stats.data.value?.roles ?? []).map((role) => ({
    label: role.name || '—',
    value: role.approved,
  })),
])
const ratingsCount = computed(() => stats.data.value?.ratingsCount ?? 0)
const ratingsAverage = computed(() => {
  const average = stats.data.value?.ratingsAverage
  return average === null || average === undefined ? '—' : formatNumber(Number(average.toFixed(1)))
})

const modalityOptions = computed(() => toSelectOptions(modalities.data.value))

function filterByModality(value: string | boolean | null): void {
  modalityId.value = typeof value === 'string' ? value : null
}

function openBadges(): void {
  void router.push({ name: 'admin-event-badges', params: { eventId: props.eventId } })
}

function openRoster(): void {
  void router.push({ name: 'admin-event-roster', params: { eventId: props.eventId } })
}
</script>

<template>
  <div>
    <BrandButton variant="back" :to="{ name: 'admin-events' }" class="back">
      {{ $t('pages.admin.eventDetail.back') }}
    </BrandButton>

    <AdminPageHeader
      :title="event.data.value?.title ?? $t('pages.admin.eventDetail.headerFallback')"
      :subtitle="event.data.value?.subtitle ?? ''"
    >
      <template #actions>
        <ActionButton
          :label="$t('pages.admin.eventDetail.newActivity')"
          icon="plus"
          :disabled="loading"
          @click="openCreate"
        />
        <ActionButton
          :label="$t('pages.admin.eventDetail.printBadges')"
          icon="print"
          @click="openBadges"
        />
        <ActionButton
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
        <DataTable
          :table="table"
          :empty-text="$t('pages.admin.eventDetail.empty.none')"
          :error-text="$t('pages.admin.eventDetail.empty.error')"
        >
          <el-table-column :label="$t('common.image')" width="110">
            <template #default="{ row }">
              <ListThumbnail :thumbnail-id="row.thumbnailId" :alt="row.title" style="width: 88px" />
            </template>
          </el-table-column>
          <el-table-column prop="title" min-width="200" sortable="custom">
            <template #header>
              <ColumnSearch
                v-model="table.columnFilter('title').value"
                :label="$t('pages.admin.eventDetail.columns.title')"
                :placeholder="$t('pages.admin.eventDetail.columns.searchTitle')"
                @apply="table.onFilter"
              />
            </template>
          </el-table-column>
          <el-table-column prop="activityStartsAt" min-width="210" sortable="custom">
            <template #header>
              <ColumnFilterDate
                v-model="table.columnFilter('activityDate').value"
                :label="$t('pages.admin.eventDetail.columns.schedule')"
                @apply="table.onFilter"
              />
            </template>
            <template #default="{ row }">
              {{ formatDateTimeRange(row.startsAt, row.endsAt) }}
            </template>
          </el-table-column>
          <el-table-column prop="modalityName" min-width="180" sortable="custom">
            <template #header>
              <ColumnFilterSelect
                :model-value="modalityId"
                :label="$t('pages.admin.eventDetail.columns.modality')"
                :options="modalityOptions"
                @update:model-value="filterByModality"
              />
            </template>
            <template #default="{ row }">
              <div class="modality-cell">
                <span class="modality-cell__type">{{ row.modality }}</span>
                <span class="modality-cell__loc">{{ row.location }}</span>
              </div>
            </template>
          </el-table-column>
          <el-table-column :label="$t('common.actions')" width="120" align="center" fixed="right">
            <template #default="{ row }">
              <div class="ca-row-actions">
                <ActionButton
                  icon="pencil"
                  type="success"
                  text
                  circle
                  :aria-label="$t('common.edit')"
                  :disabled="loading"
                  @click="openEdit(row)"
                />
                <ActionButton
                  icon="trash"
                  text
                  circle
                  type="danger"
                  :aria-label="$t('common.delete')"
                  @click="confirmRemove(row)"
                />
              </div>
            </template>
          </el-table-column>
        </DataTable>
      </el-tab-pane>
      <el-tab-pane :label="$t('pages.admin.eventDetail.tabs.attendees')" name="attendees">
        <EventAttendeesTab :event-id="eventId" :active="activeTab === 'attendees'" />
      </el-tab-pane>
      <el-tab-pane :label="$t('pages.admin.eventDetail.tabs.opinions')" name="opinions">
        <EventOpinionsTab :event-id="eventId" :active="activeTab === 'opinions'" />
      </el-tab-pane>
    </el-tabs>

    <ActivityFormDialog
      v-model:visible="visible"
      :activity="editing"
      :modalities="modalities.data.value ?? []"
      :roles="roles.data.value ?? []"
      :saving="saving"
      :event-start="event.data.value?.startsAt ?? null"
      :event-end="event.data.value?.endsAt ?? null"
      @submit="save"
    />
  </div>
</template>

<style scoped>
.back {
  margin-bottom: 4px;
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
