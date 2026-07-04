<script setup lang="ts">
import { ContentEntityPage, useContentEntity } from '@/widgets/content-entity-page'
import {
  deleteApiResourcesResourceId,
  getApiResources,
  getApiResourcesResourceId,
  postApiResources,
  putApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import type { GetApiResourcesParams } from '@/shared/api/generated/models'
import { toPage, unwrapOrNull } from '@/shared/api'
import { resourceQueryKeys } from '@/entities/resource'

const controller = useContentEntity<GetApiResourcesParams>({
  queryKey: resourceQueryKeys.all,
  fetchPage: (params) => getApiResources(params).then(toPage),
  defaultSort: { field: 'createdAt', order: -1 },
  columns: {
    title: { type: 'text' },
    subtitle: { type: 'text' },
  },
  fetchOne: (id) => unwrapOrNull(getApiResourcesResourceId(id)),
  create: (body) => postApiResources(body),
  update: (id, body) => putApiResourcesResourceId(id, body),
  remove: (id) => deleteApiResourcesResourceId(id),
})
</script>

<template>
  <ContentEntityPage
    title="Recursos"
    subtitle="Material formativo publicado"
    new-label="Nuevo recurso"
    entity-label="recurso"
    :controller="controller"
  />
</template>
