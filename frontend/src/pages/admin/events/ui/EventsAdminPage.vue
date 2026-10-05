<script setup lang="ts">
import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { eventCategoryQueries } from '@/entities/event-category'
import { formatDateRange, formatDateTime, formatDateTimeRange } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColorTag } from '@/shared/ui/color-tag'
import {
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
  toSelectOptions,
} from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { ListThumbnail } from '@/shared/ui/list-thumbnail'

import { useEventsAdmin } from '../model/use-events-admin'
import EventFormDialog from './EventFormDialog.vue'

const { table, dialog, saving, featuring, save, feature, confirmRemove } = useEventsAdmin()
const { visible, editing, loading, openCreate, openEdit } = dialog
const categories = useQuery(eventCategoryQueries.options())
const categoryOptions = computed(() => toSelectOptions(categories.data.value))
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
      :empty-text="$t('pages.admin.events.empty.none')"
      :error-text="$t('pages.admin.events.empty.error')"
    >
      <el-table-column :label="$t('common.image')" width="110">
        <template #default="{ row }">
          <ListThumbnail :thumbnail-id="row.thumbnailId" :alt="row.title" style="width: 88px" />
        </template>
      </el-table-column>

      <el-table-column prop="title" sortable="custom" min-width="200">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('search').value"
            :label="$t('pages.admin.events.columns.event')"
            :placeholder="$t('pages.admin.events.columns.searchEvent')"
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
          <small class="subtitle-cell">{{ row.subtitle }}</small>
        </template>
      </el-table-column>

      <el-table-column prop="categories" sortable="custom" min-width="150">
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
              v-for="category in row.categories"
              :key="category.id"
              :value="category.name"
              :color="category.color"
            />
            <span v-if="!row.categories.length">—</span>
          </div>
        </template>
      </el-table-column>

      <el-table-column prop="eventStartsAt" sortable="custom" min-width="140">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('eventDate').value"
            :label="$t('pages.admin.events.columns.duration')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          {{ formatDateRange(row.startsAt, row.endsAt) }}
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

      <el-table-column
        :label="$t('common.actions')"
        width="170"
        align="center"
        :fixed="table.actionsFixed.value"
      >
        <template #default="{ row }">
          <div class="ca-row-actions">
            <ActionButton
              :icon="row.featured ? 'star-fill' : 'star'"
              text
              circle
              type="warning"
              :aria-label="
                row.featured
                  ? $t('pages.admin.events.aria.featured')
                  : $t('pages.admin.events.aria.feature')
              "
              :disabled="row.featured || featuring"
              :class="{ 'is-featured': row.featured }"
              @click="feature(row)"
            />
            <RouterLink :to="{ name: 'admin-event-detail', params: { eventId: row.id } }">
              <ActionButton
                icon="cog"
                text
                circle
                type="primary"
                class="ca-action-icon--manage"
                :aria-label="$t('pages.admin.events.aria.manage')"
              />
            </RouterLink>
            <ActionButton
              icon="pencil"
              text
              circle
              type="success"
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

    <EventFormDialog v-model:visible="visible" :event="editing" :saving="saving" @submit="save" />
  </div>
</template>

<style scoped>
.title-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.subtitle-cell {
  display: block;
  margin-top: 2px;
  color: var(--ca-text-muted);
}

.cats-cell {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.ca-row-actions :deep(.is-featured .el-icon) {
  color: var(--ca-action-yellow);
}

.signup-early {
  display: block;
  color: var(--ca-text-muted);
  font-size: 12px;
}
</style>
