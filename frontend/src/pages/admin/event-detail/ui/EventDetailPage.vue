<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import { activityQueries, type ActivityListing } from '@/entities/activity'
import { eventQueries } from '@/entities/event'
import { formatDate, formatTimeRange } from '@/shared/lib/date'
import { formatScore } from '@/shared/lib/number'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { BrandButton } from '@/shared/ui/brand-button'
import { ColumnFilterDate, ColumnFilterSelect, toSelectOptions } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { eventReportQueries } from '../api/queries'
import { useEventActivitiesAdmin } from '../model/use-event-activities-admin'
import ActivityFormDialog from './ActivityFormDialog.vue'
import EventAttendeesTab from './EventAttendeesTab.vue'
import EventOpinionsTab from './EventOpinionsTab.vue'
import EventTermsTab from './EventTermsTab.vue'

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
const { table, dialog, saving, save, confirmRemove } = useEventActivitiesAdmin(() => props.eventId)
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
const scoredRatingsCount = computed(() => stats.data.value?.scoredRatingsCount ?? 0)
const ratingsAverage = computed(() => formatScore(stats.data.value?.ratingsAverage))

const modalityOptions = computed(() => toSelectOptions(modalities.data.value))

function activityActions(activity: ActivityListing): RowAction[] {
  return [
    {
      key: 'edit',
      label: t('common.edit'),
      icon: 'pencil',
      inline: true,
      disabled: loading.value,
      run: () => void openEdit(activity),
    },
    {
      key: 'delete',
      label: t('common.delete'),
      icon: 'trash',
      inline: true,
      danger: true,
      run: () => {
        confirmRemove(activity)
      },
    },
  ]
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
          <span v-if="scoredRatingsCount > 0" class="summary__unit">/5</span>
        </div>
        <div class="summary__label">
          {{ $t('pages.admin.eventDetail.summary.ratings', scoredRatingsCount) }}
        </div>
      </div>
    </div>

    <el-tabs v-model="activeTab" class="tabs">
      <el-tab-pane :label="$t('pages.admin.eventDetail.tabs.activities')" name="activities">
        <DataTable
          :table="table"
          :empty-text="$t('pages.admin.eventDetail.empty.none')"
          :error-text="$t('pages.admin.eventDetail.empty.error')"
          :search="{ key: 'title', label: $t('pages.admin.eventDetail.columns.searchTitle') }"
        >
          <template #actions>
            <ActionButton
              :label="$t('pages.admin.eventDetail.newActivity')"
              icon="plus"
              size="small"
              :disabled="loading"
              @click="openCreate"
            />
          </template>
          <el-table-column
            prop="title"
            :label="$t('pages.admin.eventDetail.columns.title')"
            min-width="240"
            sortable="custom"
            :fixed="table.isNarrow.value ? false : 'left'"
          >
            <template #default="{ row }">
              <TablePrimaryCell :title="row.title" :subtitle="row.location">
                <template #leading>
                  <ListThumbnail size="table" :thumbnail-id="row.thumbnailId" />
                </template>
              </TablePrimaryCell>
            </template>
          </el-table-column>
          <el-table-column
            prop="activityStartsAt"
            :label="$t('pages.admin.eventDetail.columns.schedule')"
            width="170"
            sortable="custom"
          >
            <template #header>
              <ColumnFilterDate
                v-model="table.columnFilter('activityDate').value"
                :label="$t('pages.admin.eventDetail.columns.schedule')"
                @apply="table.onFilter"
              />
            </template>
            <template #default="{ row }">
              <div class="schedule-cell">
                <span>{{ formatDate(row.startsAt) }}</span>
                <span class="schedule-cell__time">{{
                  formatTimeRange(row.startsAt, row.endsAt, row.startsAt)
                }}</span>
              </div>
            </template>
          </el-table-column>
          <el-table-column
            prop="modalityName"
            :label="$t('pages.admin.eventDetail.columns.modality')"
            width="150"
            sortable="custom"
            show-overflow-tooltip
          >
            <template #header>
              <ColumnFilterSelect
                v-model="table.columnFilter('modality').value"
                :label="$t('pages.admin.eventDetail.columns.modality')"
                :options="modalityOptions"
                @apply="table.onFilter"
              />
            </template>
            <template #default="{ row }">{{ row.modality }}</template>
          </el-table-column>
          <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
            <template #default="{ row }">
              <RowActions :actions="activityActions(row)" />
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
      <el-tab-pane :label="$t('pages.admin.eventDetail.tabs.terms')" name="terms">
        <EventTermsTab
          :event-id="eventId"
          :documents="event.data.value?.terms ?? []"
          :active="activeTab === 'terms'"
        />
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

.schedule-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
  white-space: nowrap;
}

.schedule-cell > span {
  overflow: hidden;
  text-overflow: ellipsis;
}

.schedule-cell__time {
  font-size: 12.5px;
  line-height: 18px;
  color: var(--ca-text-muted);
}
</style>
