<script setup lang="ts">
import { computed, type Component } from 'vue'
import {
  ArrowDown,
  ArrowRight,
  BottomRight,
  Calendar,
  CircleCheckFilled,
  CircleClose,
  CircleCloseFilled,
  Clock,
  Close,
  Comment,
  CreditCard,
  Delete,
  Document,
  Download,
  EditPen,
  Filter,
  Grid,
  Histogram,
  InfoFilled,
  Link,
  Loading,
  LocationFilled,
  Message,
  Minus,
  Moon,
  Notebook,
  OfficeBuilding,
  Paperclip,
  Phone,
  Picture,
  Plus,
  Postcard,
  PriceTag,
  Printer,
  Promotion,
  Refresh,
  RefreshLeft,
  RefreshRight,
  Search,
  Setting,
  Star,
  StarFilled,
  Sunny,
  TopRight,
  User,
  WarnTriangleFilled,
} from '@element-plus/icons-vue'

interface CustomIcon {
  readonly fill?: readonly string[]
  readonly stroke?: readonly string[]
}

const ELEMENT_ICONS: Record<string, Component> = {
  'arrow-down-right': BottomRight,
  'arrow-up-right': TopRight,
  book: Notebook,
  building: OfficeBuilding,
  calendar: Calendar,
  card: CreditCard,
  'chart-bar': Histogram,
  'check-circle': CircleCheckFilled,
  'chevron-down': ArrowDown,
  'chevron-right': ArrowRight,
  clock: Clock,
  cog: Setting,
  comment: Comment,
  download: Download,
  envelope: Message,
  error: CircleCloseFilled,
  'exclamation-triangle': WarnTriangleFilled,
  file: Document,
  filter: Filter,
  grid: Grid,
  'id-card': Postcard,
  image: Picture,
  'info-circle': InfoFilled,
  link: Link,
  'map-marker': LocationFilled,
  minus: Minus,
  moon: Moon,
  paperclip: Paperclip,
  pencil: EditPen,
  phone: Phone,
  plus: Plus,
  print: Printer,
  refresh: Refresh,
  search: Search,
  send: Promotion,
  spinner: Loading,
  star: Star,
  'star-fill': StarFilled,
  sun: Sunny,
  sync: RefreshRight,
  tag: PriceTag,
  times: Close,
  'times-circle': CircleClose,
  trash: Delete,
  undo: RefreshLeft,
  user: User,
}

const CUSTOM_ICONS: Record<string, CustomIcon> = {
  'align-left': {
    stroke: ['M3 5h18', 'M3 9.7h12', 'M3 14.3h18', 'M3 19h12'],
  },
  'align-center': {
    stroke: ['M3 5h18', 'M6 9.7h12', 'M3 14.3h18', 'M6 19h12'],
  },
  'align-right': {
    stroke: ['M3 5h18', 'M9 9.7h12', 'M3 14.3h18', 'M9 19h12'],
  },
  ban: {
    stroke: ['M21.5 12a9.5 9.5 0 1 1-19 0 9.5 9.5 0 0 1 19 0z', 'M5.28 5.28 18.72 18.72'],
  },
  bars: {
    stroke: ['M3.5 6.5h17', 'M3.5 12h17', 'M3.5 17.5h17'],
  },
  'calendar-plus': {
    stroke: [
      'M3 7.5A2.5 2.5 0 0 1 5.5 5h13A2.5 2.5 0 0 1 21 7.5v11a2.5 2.5 0 0 1-2.5 2.5h-13A2.5 2.5 0 0 1 3 18.5z',
      'M3 10.5h18',
      'M7.5 3v4',
      'M16.5 3v4',
      'M12 12.9v5.6',
      'M9.2 15.7h5.6',
    ],
  },
  'check-square': {
    stroke: [
      'M3 5.5A2.5 2.5 0 0 1 5.5 3h13A2.5 2.5 0 0 1 21 5.5v13a2.5 2.5 0 0 1-2.5 2.5h-13A2.5 2.5 0 0 1 3 18.5z',
      'm7.6 12.1 3 3 5.8-5.8',
    ],
  },
  facebook: {
    fill: [
      'M22 12.06C22 6.5 17.52 2 12 2S2 6.5 2 12.06c0 5.02 3.66 9.18 8.44 9.94v-7.03H7.9v-2.91h2.54V9.85c0-2.52 1.49-3.91 3.77-3.91 1.09 0 2.24.2 2.24.2v2.46h-1.26c-1.24 0-1.63.78-1.63 1.57v1.89h2.78l-.45 2.91h-2.33V22c4.78-.76 8.44-4.92 8.44-9.94z',
    ],
  },
  home: {
    stroke: ['M3.5 10.4 12 3.5l8.5 6.9', 'M5.6 12v7.5a1 1 0 0 0 1 1h10.8a1 1 0 0 0 1-1V12'],
  },
  'filter-fill': {
    fill: ['M3 4h18l-7 8.4V20l-4-2v-5.6z'],
  },
  instagram: {
    fill: [
      'M7.8 2h8.4A5.8 5.8 0 0 1 22 7.8v8.4a5.8 5.8 0 0 1-5.8 5.8H7.8A5.8 5.8 0 0 1 2 16.2V7.8A5.8 5.8 0 0 1 7.8 2m0 2A3.8 3.8 0 0 0 4 7.8v8.4A3.8 3.8 0 0 0 7.8 20h8.4a3.8 3.8 0 0 0 3.8-3.8V7.8A3.8 3.8 0 0 0 16.2 4z',
      'M12 6.9a5.1 5.1 0 1 1 0 10.2 5.1 5.1 0 0 1 0-10.2m0 2a3.1 3.1 0 1 0 0 6.2 3.1 3.1 0 0 0 0-6.2',
      'M17.6 5.1a1.35 1.35 0 1 1 0 2.7 1.35 1.35 0 0 1 0-2.7',
    ],
  },
  linkedin: {
    fill: [
      'M6.94 5a1.94 1.94 0 1 1-3.88 0 1.94 1.94 0 0 1 3.88 0',
      'M3.32 8.98h3.24V20H3.32z',
      'M9.1 8.98h3.11v1.5h.04a3.4 3.4 0 0 1 3.07-1.68c3.28 0 3.89 2.16 3.89 4.98V20h-3.24v-4.9c0-1.21-.02-2.77-1.69-2.77-1.69 0-1.95 1.32-1.95 2.68V20H9.1z',
    ],
  },
  list: {
    fill: [
      'M4.6 4.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8',
      'M4.6 10.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8',
      'M4.6 16.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8',
    ],
    stroke: ['M9.4 6h11.6', 'M9.4 12h11.6', 'M9.4 18h11.6'],
  },
  megaphone: {
    stroke: [
      'M3.5 9.4h3.7l9.2-4.85a1 1 0 0 1 1.47.88v13.14a1 1 0 0 1-1.47.88L7.2 14.6H3.5A1.5 1.5 0 0 1 2 13.1v-2.2a1.5 1.5 0 0 1 1.5-1.5z',
      'M8.6 14.8 10 19.6a1.6 1.6 0 0 0 2.88-.84l-1.16-3.98',
      'M20.8 9.6a3.5 3.5 0 0 1 0 4.8',
    ],
  },
  palette: {
    fill: [
      'M7.1 11a1.35 1.35 0 1 1 0 2.7 1.35 1.35 0 0 1 0-2.7',
      'M9.5 6.9a1.35 1.35 0 1 1 0 2.7 1.35 1.35 0 0 1 0-2.7',
      'M14.3 6.6a1.35 1.35 0 1 1 0 2.7 1.35 1.35 0 0 1 0-2.7',
      'M17.2 9.1a1.35 1.35 0 1 1 0 2.7 1.35 1.35 0 0 1 0-2.7',
    ],
    stroke: [
      'M12 3.2a8.8 8.8 0 0 0 0 17.6 1.9 1.9 0 0 0 1.9-1.9c0-.5-.2-.95-.53-1.3a1.9 1.9 0 0 1 1.38-3.2h2.09a4.16 4.16 0 0 0 4.16-4.16c0-3.98-4.05-7.04-9-7.04z',
    ],
  },
  'sort-amount-down': {
    stroke: [
      'M6 3.5v17',
      'm2.6 17.1 3.4 3.4 3.4-3.4',
      'M12.5 5h8.9',
      'M12.5 10h6.7',
      'M12.5 15h4.5',
      'M12.5 20h2.3',
    ],
  },
  'sort-amount-up-alt': {
    stroke: [
      'M6 20.5v-17',
      'm2.6 6.9 3.4-3.4 3.4 3.4',
      'M12.5 5h2.3',
      'M12.5 10h4.5',
      'M12.5 15h6.7',
      'M12.5 20h8.9',
    ],
  },
  table: {
    stroke: [
      'M3 5.5A2.5 2.5 0 0 1 5.5 3h13A2.5 2.5 0 0 1 21 5.5v13a2.5 2.5 0 0 1-2.5 2.5h-13A2.5 2.5 0 0 1 3 18.5z',
      'M3 9h18',
      'M3 15h18',
      'M10.2 9v12',
      'M15.6 9v12',
    ],
  },
  users: {
    stroke: [
      'M9.2 4.8a3.55 3.55 0 1 1 0 7.1 3.55 3.55 0 0 1 0-7.1z',
      'M2.6 20.4a6.6 6.6 0 0 1 13.2 0',
      'M16.1 5.1a3.55 3.55 0 0 1 0 6.5',
      'M17.6 14.6a6.6 6.6 0 0 1 3.8 5.8',
    ],
  },
}

defineOptions({ inheritAttrs: false })

const props = defineProps<{ name: string; spin?: boolean }>()

const elementIcon = computed<Component | undefined>(() => ELEMENT_ICONS[props.name])
const customIcon = computed<CustomIcon | undefined>(() => CUSTOM_ICONS[props.name])
</script>

<template>
  <el-icon
    v-if="elementIcon"
    aria-hidden="true"
    v-bind="$attrs"
    class="app-icon"
    :class="{ 'app-icon--spin': spin }"
  >
    <component :is="elementIcon" />
  </el-icon>
  <el-icon
    v-else-if="customIcon"
    aria-hidden="true"
    v-bind="$attrs"
    class="app-icon"
    :class="{ 'app-icon--spin': spin }"
  >
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
      <path
        v-for="(d, index) in customIcon.fill ?? []"
        :key="`fill-${index}`"
        :d="d"
        fill="currentColor"
        fill-rule="evenodd"
      />
      <path
        v-for="(d, index) in customIcon.stroke ?? []"
        :key="`stroke-${index}`"
        :d="d"
        fill="none"
        stroke="currentColor"
        stroke-width="1.6"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </svg>
  </el-icon>
</template>

<style scoped>
.app-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1em;
  height: 1em;
  line-height: 1em;
  font-size: inherit;
  color: inherit;
  fill: currentColor;
  vertical-align: -0.125em;
}

.app-icon svg {
  width: 1em;
  height: 1em;
}

.app-icon--spin {
  animation: app-icon-spin 2s linear infinite;
}

@keyframes app-icon-spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}
</style>
