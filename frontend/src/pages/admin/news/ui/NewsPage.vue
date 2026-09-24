<script setup lang="ts">
import { ContentEntityPage, useContentEntity } from '@/widgets/content-entity-page'
import type { GetApiNewsParams } from '@/shared/api/generated/models'
import {
  createNewsItemRequest,
  deleteNewsItemRequest,
  getNewsAdminPageRequest,
  getNewsItemAdminRequest,
  newsQueryKeys,
  toggleNewsItemFeatureRequest,
  updateNewsItemRequest,
} from '@/entities/news-item'

const controller = useContentEntity<GetApiNewsParams>({
  queryKey: newsQueryKeys.all,
  fetchPage: (params) => getNewsAdminPageRequest(params),
  defaultSort: { field: 'createdAt', order: -1 },
  columns: {
    title: { type: 'text' },
    subtitle: { type: 'text' },
    created: { type: 'dateRange', fromParam: 'createdFrom', toParam: 'createdTo' },
  },
  fetchOne: (id) => getNewsItemAdminRequest(id),
  create: (body) => createNewsItemRequest(body),
  update: (id, body) => updateNewsItemRequest(id, body),
  remove: (id) => deleteNewsItemRequest(id),
  feature: (id) => toggleNewsItemFeatureRequest(id),
})
</script>

<template>
  <ContentEntityPage
    :title="$t('pages.admin.news.title')"
    :subtitle="$t('pages.admin.news.subtitle')"
    :new-label="$t('pages.admin.news.newLabel')"
    :entity-label="$t('pages.admin.news.entityLabel')"
    :controller="controller"
  />
</template>
