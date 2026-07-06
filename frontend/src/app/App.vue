<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'

import AdminLayout from '@/app/layouts/AdminLayout.vue'
import BlankLayout from '@/app/layouts/BlankLayout.vue'
import DefaultLayout from '@/app/layouts/DefaultLayout.vue'
import { useAuth } from '@/features/auth'

const route = useRoute()
const { bootstrap } = useAuth()

const layout = computed(() => {
  if (route.meta.layout === 'admin') return AdminLayout
  if (route.meta.layout === 'blank') return BlankLayout
  return DefaultLayout
})

onMounted(() => {
  void bootstrap()
})
</script>

<template>
  <component :is="layout">
    <RouterView v-slot="{ Component }">
      <component :is="Component" />
    </RouterView>
  </component>
</template>
