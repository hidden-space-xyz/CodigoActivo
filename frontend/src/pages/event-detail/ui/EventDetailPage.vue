<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'

import { eventQueries, signupAccess, statusLabelKey } from '@/entities/event'
import { useSession } from '@/entities/session'
import { fileContentUrl } from '@/shared/api'
import { formatDateRange, formatDateTime, formatDateTimeRange } from '@/shared/lib/date'
import { isRichTextEmpty, richTextExcerpt } from '@/shared/lib/rich-text'
import { absoluteUrl, type SeoData, useSeo } from '@/shared/lib/seo'
import { BrandButton } from '@/shared/ui/brand-button'
import { ColorTag } from '@/shared/ui/color-tag'
import { RichTextContent } from '@/shared/ui/rich-text-content'

import { useLeaderRoster } from '../model/use-leader-roster'
import EventActivitiesTimeline from './EventActivitiesTimeline.vue'
import LeaderRosterPanel from './LeaderRosterPanel.vue'

const props = defineProps<{
  /** Event id from the `/events/:eventId` route param. */
  eventId: string
}>()

const route = useRoute()
const { t } = useI18n()
const { data: event, isLoading } = useQuery(() => eventQueries.detail(props.eventId))
const notFound = computed(() => !isLoading.value && event.value === null)
const session = useSession()

const tab = ref<'info' | 'activities' | 'attendees'>('info')

const { hasActivities: canSeeAttendees } = useLeaderRoster(() => props.eventId)

watch(canSeeAttendees, (visible) => {
  if (!visible && tab.value === 'attendees') tab.value = 'info'
})

const hasDescription = computed(() => !isRichTextEmpty(event.value?.description))

const access = computed(() =>
  event.value ? signupAccess(event.value, session.user?.earlySignupEligible ?? false) : 'closed',
)
const signupAllowed = computed(() => access.value === 'open')
const earlySignupOnly = computed(() => access.value === 'earlyOnly')

const infoRows = computed(() => {
  const current = event.value
  if (!current) return []
  return [
    {
      label: t('pages.eventDetail.info.date'),
      value: formatDateRange(current.startsAt, current.endsAt),
    },
    ...(current.earlySignupStartsAt
      ? [
          {
            label: t('pages.eventDetail.info.earlySignup'),
            value: formatDateTime(current.earlySignupStartsAt),
          },
        ]
      : []),
    {
      label: t('pages.eventDetail.info.signup'),
      value: formatDateTimeRange(current.signupStartsAt, current.signupEndsAt),
    },
    { label: t('common.status'), value: t(statusLabelKey(current.status)) },
  ]
})

const posterUrl = computed(() => fileContentUrl(event.value?.thumbnailId))

const seo = computed<SeoData | undefined>(() => {
  if (notFound.value) return { title: t('pages.eventDetail.seo.notFound'), noindex: true }
  const current = event.value
  if (!current) return undefined
  const description = richTextExcerpt(current.description) || current.subtitle
  return {
    title: current.title,
    description,
    image: posterUrl.value,
    type: 'article',
    jsonLd: {
      '@context': 'https://schema.org',
      '@type': 'Event',
      name: current.title,
      url: absoluteUrl(route.path),
      startDate: current.startsAt,
      endDate: current.endsAt,
      description,
      image: absoluteUrl(posterUrl.value),
      organizer: { '@type': 'Organization', name: t('seo.siteName') },
      location: {
        '@type': 'Place',
        name: t('seo.siteName'),
        address: {
          '@type': 'PostalAddress',
          addressLocality: 'León',
          addressCountry: 'ES',
        },
      },
    },
  }
})

useSeo(seo)
</script>

<template>
  <div>
    <section class="detail-back">
      <div class="ca-container--narrow">
        <BrandButton variant="back" :to="{ name: 'events' }">
          {{ $t('pages.eventDetail.backToEvents') }}
        </BrandButton>
      </div>
    </section>

    <p v-if="isLoading" class="detail-state ca-container--narrow">{{ $t('common.loading') }}</p>

    <p v-else-if="notFound || !event" class="detail-state ca-container--narrow">
      {{ $t('pages.eventDetail.notFound') }}
    </p>

    <template v-else>
      <section class="detail-head">
        <div class="ca-container--narrow">
          <h1 class="detail-head__title">{{ event.title }}</h1>
          <div class="detail-head__slogan">
            {{ $t('pages.eventDetail.slogan', { subtitle: event.subtitle }) }}
          </div>
          <div v-if="event.categories.length" class="detail-head__cats">
            <ColorTag
              v-for="cat in event.categories"
              :key="cat.id"
              :value="cat.name"
              :color="cat.color"
            />
          </div>
        </div>
      </section>

      <nav class="detail-tabs">
        <div class="ca-container--narrow detail-tabs__inner">
          <button
            type="button"
            class="detail-tab"
            :class="{ 'detail-tab--active': tab === 'info' }"
            :title="$t('pages.eventDetail.tabs.viewInfo')"
            @click="tab = 'info'"
          >
            {{ $t('pages.eventDetail.tabs.info') }}
          </button>
          <button
            type="button"
            class="detail-tab"
            :class="{ 'detail-tab--active': tab === 'activities' }"
            :title="$t('pages.eventDetail.tabs.viewActivities')"
            @click="tab = 'activities'"
          >
            {{ $t('pages.eventDetail.tabs.activities') }}
          </button>
          <button
            v-if="canSeeAttendees"
            type="button"
            class="detail-tab"
            :class="{ 'detail-tab--active': tab === 'attendees' }"
            :title="$t('pages.eventDetail.tabs.viewAttendees')"
            @click="tab = 'attendees'"
          >
            {{ $t('pages.eventDetail.tabs.attendees') }}
          </button>
        </div>
      </nav>

      <section v-if="tab === 'info'" class="detail-body">
        <div class="ca-container--narrow detail-body__grid">
          <div class="detail-body__main">
            <img :src="posterUrl" :alt="event.title" class="detail-body__poster" />

            <h2 class="detail-body__h2">{{ $t('pages.eventDetail.aboutEvent') }}</h2>
            <RichTextContent v-if="hasDescription" :content="event.description" />
            <p v-else class="detail-body__p detail-body__p--muted">
              {{ $t('pages.eventDetail.noDescription') }}
            </p>
          </div>

          <aside class="detail-body__panel">
            <h3 class="detail-panel__title">{{ $t('pages.eventDetail.tabs.info') }}</h3>
            <dl class="detail-panel__info">
              <div v-for="row in infoRows" :key="row.label" class="detail-panel__row">
                <dt class="detail-panel__label">{{ row.label }}</dt>
                <dd class="detail-panel__value">{{ row.value }}</dd>
              </div>
            </dl>
            <p class="detail-panel__note">{{ $t('pages.eventDetail.panelNote') }}</p>
            <BrandButton variant="primary" block @click="tab = 'activities'">
              {{ $t('pages.eventDetail.tabs.viewActivities') }}
            </BrandButton>
          </aside>
        </div>
      </section>

      <section v-else-if="tab === 'activities'" class="detail-body">
        <div class="ca-container--narrow">
          <EventActivitiesTimeline
            :event-id="eventId"
            :signup-open="signupAllowed"
            :early-only="earlySignupOnly"
            :terms="event.terms"
          />
        </div>
      </section>

      <section v-else-if="canSeeAttendees" class="detail-body">
        <div class="ca-container--narrow">
          <LeaderRosterPanel :event-id="eventId" />
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.detail-back {
  padding: 36px var(--ca-gutter) 0;
}

.detail-state {
  padding: 40px var(--ca-gutter);
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.detail-head {
  padding: 24px var(--ca-gutter) 16px;
}

.detail-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: clamp(28px, 6vw, 46px);
  line-height: 1.05;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
  margin-top: 16px;
}

.detail-head__slogan {
  font-family: var(--ca-font-display);
  font-size: 23px;
  margin-top: 8px;
  color: var(--ca-text-muted);
}

.detail-head__cats {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 14px;
}

.detail-tabs {
  padding: 0 24px;
  border-bottom: 1px solid var(--ca-border);
}

.detail-tabs__inner {
  display: flex;
  gap: 8px;
}

.detail-tab {
  position: relative;
  background: transparent;
  border: none;
  padding: 12px 6px;
  margin-right: 18px;
  font-family: var(--ca-font-display);
  font-size: 16px;
  font-weight: 600;
  color: var(--ca-text-muted);
  cursor: pointer;
  transition: color 0.15s ease;
}

.detail-tab:hover {
  color: var(--ca-text);
}

.detail-tab--active {
  color: var(--ca-text-bright);
}

.detail-tab--active::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: -1px;
  height: 2px;
  border-radius: 2px;
  background: var(--ca-orange);
}

.detail-body {
  padding: 28px var(--ca-gutter) 80px;
}

.detail-body__grid {
  display: grid;
  grid-template-columns: 1.3fr 0.7fr;
  gap: 40px;
  align-items: start;
}

.detail-body__poster {
  width: 100%;
  height: auto;
  border-radius: 18px;
  border: 1px solid var(--ca-border);
  margin-bottom: 28px;
  display: block;
}

.detail-body__h2 {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 24px;
  color: var(--ca-text-bright);
}

.detail-body__p {
  margin-top: 12px;
  font-size: 16.5px;
  line-height: 1.7;
  color: var(--ca-text-muted);
  white-space: pre-line;
}

.detail-body__p--muted {
  color: var(--ca-text-dim);
  font-style: italic;
}

.detail-body__panel {
  position: sticky;
  top: 90px;
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 26px;
  box-shadow: 0 24px 60px var(--ca-shadow-lg);
}

.detail-panel__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 19px;
  color: var(--ca-text-bright);
  margin-bottom: 16px;
}

.detail-panel__info {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.detail-panel__row {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.detail-panel__label {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.detail-panel__value {
  margin: 0;
  font-weight: 600;
  color: var(--ca-text);
}

.detail-panel__note {
  margin: 22px 0 16px;
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--ca-text-dim);
}

@media (max-width: 1024px) {
  .detail-body__grid {
    grid-template-columns: 1fr;
  }
  .detail-body__panel {
    position: static;
  }
}
</style>
