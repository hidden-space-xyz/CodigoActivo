<script setup lang="ts">
import { ContentEntityPage, useContentEntity } from '@/widgets/content-entity-page'
import {
  deleteApiResourcesResourceId,
  getApiResources,
  getApiResourcesResourceId,
  postApiResources,
  putApiResourcesResourceId,
} from '@/shared/api/generated/endpoints/resources/resources'
import { resourceQueryKeys } from '@/entities/resource'

const controller = useContentEntity({
  queryKey: resourceQueryKeys.all,
  fetchAll: (signal) => getApiResources({ signal }).then((r) => r.data ?? []),
  fetchOne: (id) => getApiResourcesResourceId(id).then((r) => r.data),
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
