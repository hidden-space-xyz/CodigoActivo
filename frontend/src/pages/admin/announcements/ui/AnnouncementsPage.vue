<script setup lang="ts">
import { ContentEntityPage, useContentEntity } from '@/widgets/content-entity-page'
import type { GetApiAnnouncementsParams } from '@/shared/api/generated/models'
import {
  announcementQueryKeys,
  createAnnouncementRequest,
  deleteAnnouncementRequest,
  getAnnouncementAdminRequest,
  getAnnouncementsAdminPageRequest,
  toggleAnnouncementFeatureRequest,
  updateAnnouncementRequest,
} from '@/entities/announcement'

const controller = useContentEntity<GetApiAnnouncementsParams>({
  queryKey: announcementQueryKeys.all,
  fetchPage: (params) => getAnnouncementsAdminPageRequest(params),
  defaultSort: { field: 'createdAt', order: -1 },
  columns: {
    title: { type: 'text' },
    subtitle: { type: 'text' },
    created: { type: 'dateRange', fromParam: 'createdFrom', toParam: 'createdTo' },
  },
  fetchOne: (id) => getAnnouncementAdminRequest(id),
  create: (body) => createAnnouncementRequest(body),
  update: (id, body) => updateAnnouncementRequest(id, body),
  remove: (id) => deleteAnnouncementRequest(id),
  feature: (id) => toggleAnnouncementFeatureRequest(id),
})
</script>

<template>
  <ContentEntityPage
    :title="$t('pages.admin.announcements.title')"
    :subtitle="$t('pages.admin.announcements.subtitle')"
    :new-label="$t('pages.admin.announcements.newLabel')"
    :entity-label="$t('pages.admin.announcements.entityLabel')"
    :controller="controller"
  />
</template>
