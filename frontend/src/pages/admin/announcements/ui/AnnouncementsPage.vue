<script setup lang="ts">
import { ContentEntityPage, useContentEntity } from '@/widgets/content-entity-page'
import {
  deleteApiAnnouncementsAnnouncementId,
  patchApiAnnouncementsAnnouncementIdFeature,
  postApiAnnouncements,
  putApiAnnouncementsAnnouncementId,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import { fetchODataEntity } from '@/shared/api'
import type { AnnouncementResponse } from '@/shared/api/generated/models'
import { announcementQueryKeys } from '@/entities/announcement'

const controller = useContentEntity({
  resource: 'Announcements',
  queryKey: announcementQueryKeys.all,
  defaultSort: { field: 'createdAt', order: -1 },
  columns: {
    title: { type: 'text' },
    subtitle: { type: 'text' },
    createdAt: { type: 'datetime' },
  },
  fetchOne: (id) =>
    fetchODataEntity<AnnouncementResponse>('Announcements', id).then((r) => r ?? {}),
  create: (body) => postApiAnnouncements(body),
  update: (id, body) => putApiAnnouncementsAnnouncementId(id, body),
  remove: (id) => deleteApiAnnouncementsAnnouncementId(id),
  feature: (id) => patchApiAnnouncementsAnnouncementIdFeature(id),
})
</script>

<template>
  <ContentEntityPage
    title="Anuncios"
    subtitle="Comunicaciones para la comunidad"
    new-label="Nuevo anuncio"
    entity-label="anuncio"
    :controller="controller"
  />
</template>
