<script setup lang="ts">
import { ContentEntityPage, useContentEntity } from '@/widgets/content-entity-page'
import {
  deleteApiAnnouncementsAnnouncementId,
  getApiAnnouncements,
  getApiAnnouncementsAnnouncementId,
  patchApiAnnouncementsAnnouncementIdFeature,
  postApiAnnouncements,
  putApiAnnouncementsAnnouncementId,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import type { GetApiAnnouncementsParams } from '@/shared/api/generated/models'
import { unwrapOrNull } from '@/shared/api'
import { announcementQueryKeys } from '@/entities/announcement'

const controller = useContentEntity<GetApiAnnouncementsParams>({
  queryKey: announcementQueryKeys.all,
  fetchPage: (params) =>
    getApiAnnouncements(params).then((r) => ({
      items: r.data.items ?? [],
      total: r.data.total ?? 0,
    })),
  defaultSort: { field: 'createdAt', order: -1 },
  columns: {
    title: { type: 'text' },
    subtitle: { type: 'text' },
  },
  fetchOne: (id) => unwrapOrNull(getApiAnnouncementsAnnouncementId(id)).then((r) => r ?? {}),
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
