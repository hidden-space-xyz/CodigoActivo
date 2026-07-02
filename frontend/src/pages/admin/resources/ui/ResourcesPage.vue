<script setup lang="ts">
import { ContentEntityPage, useContentEntity } from '@/widgets/content-entity-page'
import {
  deleteApiResourcesResourceId,
  postApiResources,
  putApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import { fetchODataEntity } from '@/shared/api'
import type { ResourceResponse } from '@/shared/api/generated/models'
import { resourceQueryKeys } from '@/entities/resource'

const controller = useContentEntity({
  resource: 'Resources',
  queryKey: resourceQueryKeys.all,
  defaultSort: { field: 'createdAt', order: -1 },
  columns: {
    title: { type: 'text' },
    subtitle: { type: 'text' },
    createdAt: { type: 'datetime' },
  },
  fetchOne: (id) => fetchODataEntity<ResourceResponse>('Resources', id).then((r) => r ?? {}),
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
