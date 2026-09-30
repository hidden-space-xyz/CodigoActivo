<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useQueryClient } from '@tanstack/vue-query'

import AdminLayout from '@/app/layouts/AdminLayout.vue'
import BlankLayout from '@/app/layouts/BlankLayout.vue'
import DefaultLayout from '@/app/layouts/DefaultLayout.vue'
import { resolveSession } from '@/entities/session'

const route = useRoute()
const queryClient = useQueryClient()

const layout = computed(() => {
  if (route.meta.layout === 'admin') return AdminLayout
  if (route.meta.layout === 'blank') return BlankLayout
  return DefaultLayout
})

onMounted(() => {
  void resolveSession(queryClient)
})
</script>

<template>
  <component :is="layout">
    <RouterView v-slot="{ Component }">
      <component :is="Component" />
    </RouterView>
  </component>
</template>
