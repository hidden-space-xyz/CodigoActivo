import type { RouteRecordRaw } from 'vue-router'

export const announcementsRoutes: readonly RouteRecordRaw[] = [
  {
    path: '/announcements',
    name: 'announcements',
    component: () => import('@/modules/announcements/presentation/pages/AnnouncementsPage.vue'),
  },
  {
    path: '/announcements/:announcementId',
    name: 'announcement-detail',
    component: () =>
      import('@/modules/announcements/presentation/pages/AnnouncementDetailPage.vue'),
    props: true,
  },
]
