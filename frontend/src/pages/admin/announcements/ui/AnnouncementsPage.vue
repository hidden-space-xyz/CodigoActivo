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
import { announcementQueryKeys } from '@/entities/announcement'

const controller = useContentEntity({
  queryKey: announcementQueryKeys.all,
  fetchAll: (signal) => getApiAnnouncements({ signal }).then((r) => r.data ?? []),
  fetchOne: (id) => getApiAnnouncementsAnnouncementId(id).then((r) => r.data),
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
