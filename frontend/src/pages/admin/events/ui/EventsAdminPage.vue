<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useQuery } from '@tanstack/vue-query'

import { statusLabelKey, type EventListing } from '@/entities/event'
import { eventCategoryQueries } from '@/entities/event-category'
import { formatDateRange, formatDateTime, formatDateTimeRange } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColorTag } from '@/shared/ui/color-tag'
import { ColumnFilterDate, ColumnFilterSelect, toSelectOptions } from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { stageTagType } from '../lib/stage-tag-type'
import { useEventsAdmin } from '../model/use-events-admin'
import EventFormDialog from './EventFormDialog.vue'

const SHOWN_CATEGORIES = 2

const { t } = useI18n()
const { table, scope, setScope, dialog, saving, featuring, save, feature, confirmRemove } =
  useEventsAdmin()
const { visible, editing, loading, openCreate, openEdit } = dialog
const categories = useQuery(eventCategoryQueries.options())
const categoryOptions = computed(() => toSelectOptions(categories.data.value))

const scopeOptions = computed(() => [
  { label: t('pages.admin.events.scope.upcoming'), value: 'upcoming' },
  { label: t('pages.admin.events.scope.past'), value: 'past' },
  { label: t('pages.admin.events.scope.all'), value: 'all' },
])

const emptyText = computed(() => {
  if (scope.value === 'upcoming') return t('pages.admin.events.empty.noUpcoming')
  if (scope.value === 'past') return t('pages.admin.events.empty.noPast')
  return t('pages.admin.events.empty.none')
})

function hiddenCategoryNames(event: EventListing): string {
  return event.categories
    .slice(SHOWN_CATEGORIES)
    .map((category) => category.name)
    .join(', ')
}

function signupTitle(event: EventListing): string {
  const signupWindow = formatDateTimeRange(event.signupStartsAt, event.signupEndsAt)
  if (!event.earlySignupStartsAt) return signupWindow
  const early = t('pages.admin.events.earlySignupFrom', {
    date: formatDateTime(event.earlySignupStartsAt),
  })
  return `${signupWindow} · ${early}`
}

function eventActions(event: EventListing): RowAction[] {
  return [
    {
      key: 'edit',
      label: t('common.edit'),
      icon: 'pencil',
      inline: true,
      disabled: loading.value,
      run: () => {
        void openEdit(event)
      },
    },
    {
      key: 'feature',
      label: t('pages.admin.events.actions.feature'),
      icon: 'star',
      disabled: event.featured || featuring.value,
      ...(event.featured ? { reason: t('pages.admin.events.alreadyFeatured') } : {}),
      run: () => {
        feature(event)
      },
    },
    {
      key: 'delete',
      label: t('common.delete'),
      icon: 'trash',
      danger: true,
      divided: true,
      run: () => {
        confirmRemove(event)
      },
    },
  ]
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.events.header.title')"
      :subtitle="$t('pages.admin.events.header.subtitle')"
    >
      <template #actions>
        <ActionButton
          :label="$t('pages.admin.events.newEvent')"
          icon="plus"
          type="primary"
          :disabled="loading"
          @click="openCreate"
        />
      </template>
    </AdminPageHeader>

    <DataTable
      :table="table"
      :empty-text="emptyText"
      :error-text="$t('pages.admin.events.empty.error')"
      :search="{ key: 'search', label: $t('pages.admin.events.columns.searchEvent') }"
    >
      <template #filters>
        <el-segmented
          size="small"
          :aria-label="$t('pages.admin.events.scope.label')"
          :options="scopeOptions"
          :model-value="scope"
          @change="setScope"
        />
      </template>

      <el-table-column
        prop="title"
        sortable="custom"
        :label="$t('pages.admin.events.columns.event')"
        min-width="240"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #default="{ row }">
          <TablePrimaryCell
            :title="row.title"
            :subtitle="row.subtitle"
            :to="{ name: 'admin-event-detail', params: { eventId: row.id } }"
          >
            <template #leading>
              <ListThumbnail size="table" :thumbnail-id="row.thumbnailId" />
            </template>
            <template v-if="row.featured" #badges>
              <el-tag type="warning" size="small" disable-transitions>
                {{ $t('pages.admin.events.tag.featured') }}
              </el-tag>
            </template>
          </TablePrimaryCell>
        </template>
      </el-table-column>

      <el-table-column
        prop="categories"
        :label="$t('pages.admin.events.columns.categories')"
        width="170"
      >
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
            <span
              v-for="(category, index) in row.categories.slice(0, SHOWN_CATEGORIES)"
              :key="category.id"
              class="cats-cell__line"
            >
              <ColorTag variant="dot" :value="category.name" :color="category.color" />
              <el-tag
                v-if="index === SHOWN_CATEGORIES - 1 && row.categories.length > SHOWN_CATEGORIES"
                size="small"
                disable-transitions
                class="cats-cell__more"
                :title="hiddenCategoryNames(row)"
                >+{{ row.categories.length - SHOWN_CATEGORIES }}</el-tag
              >
            </span>
            <span v-if="!row.categories.length">—</span>
          </div>
        </template>
      </el-table-column>

      <el-table-column
        prop="eventStartsAt"
        sortable="custom"
        :label="$t('pages.admin.events.columns.dates')"
        width="170"
      >
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('eventDate').value"
            :label="$t('pages.admin.events.columns.dates')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          {{ formatDateRange(row.startsAt, row.endsAt) }}
        </template>
      </el-table-column>

      <el-table-column
        prop="signupStartsAt"
        sortable="custom"
        :label="$t('pages.admin.events.columns.signup')"
        width="190"
      >
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('signup').value"
            :label="$t('pages.admin.events.columns.signup')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <div class="signup-cell" :title="signupTitle(row)">
            <el-tag :type="stageTagType(row.status)" size="small" disable-transitions>
              {{ $t(statusLabelKey(row.status)) }}
            </el-tag>
            <span class="signup-cell__dates">{{
              formatDateRange(row.signupStartsAt, row.signupEndsAt)
            }}</span>
          </div>
        </template>
      </el-table-column>

      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions
            :actions="eventActions(row)"
            :menu-label="$t('table.moreActionsFor', { name: row.title })"
          />
        </template>
      </el-table-column>
    </DataTable>

    <EventFormDialog v-model:visible="visible" :event="editing" :saving="saving" @submit="save" />
  </div>
</template>

<style scoped>
.cats-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.cats-cell__line {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.cats-cell__more {
  flex: none;
}

.signup-cell {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
  min-width: 0;
}

.signup-cell__dates {
  max-width: 100%;
  overflow: hidden;
  font-size: 13px;
  line-height: 18px;
  color: var(--ca-text-muted);
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
