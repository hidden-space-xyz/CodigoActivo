<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import type { LeaderRosterActivity, LeaderRosterDependent } from '@/entities/event'
import { AppIcon, DataState } from '@/shared/ui'
import { formatDate, formatDateTime, formatDateTimeRange } from '@/shared/lib'

import { useLeaderRoster } from '../model/useLeaderRoster'

const props = defineProps<{
  /** Event whose led activities are listed; the API only returns the caller's own ones. */
  eventId: string
}>()

const { t } = useI18n()
const { activities, isLoading, isError } = useLeaderRoster(() => props.eventId)

function attendeesCount(activity: LeaderRosterActivity): number {
  return activity.roles.reduce((sum, role) => sum + role.users.length + role.dependents.length, 0)
}

function ageLabel(dependent: LeaderRosterDependent): string {
  if (dependent.age === null) return '—'
  return t('features.leaderRoster.ageYears', { age: dependent.age }, dependent.age)
}

function mailHref(email: string): string {
  return `mailto:${encodeURIComponent(email).replace(/%40/g, '@')}`
}

function telHref(phone: string): string {
  return `tel:${phone.replace(/[^\d+]/g, '')}`
}
</script>

<template>
  <div class="leader-roster">
    <div class="leader-roster__notice" role="note">
      <AppIcon name="info-circle" />
      <div>
        <p class="leader-roster__notice-title">{{ t('features.leaderRoster.notice.title') }}</p>
        <p class="leader-roster__notice-text">{{ t('features.leaderRoster.notice.body') }}</p>
      </div>
    </div>

    <DataState
      :loading="isLoading"
      :error="isError"
      :empty="activities.length === 0"
      :error-text="t('features.leaderRoster.loadError')"
      :empty-text="t('features.leaderRoster.empty')"
    >
      <article v-for="activity in activities" :key="activity.id" class="lr-activity">
        <header class="lr-activity__head">
          <div class="lr-activity__heading">
            <h2 class="lr-activity__title">{{ activity.title }}</h2>
            <p class="lr-activity__meta">
              <span>
                <AppIcon name="clock" />
                {{ formatDateTimeRange(activity.startsAt, activity.endsAt) }}
              </span>
              <span v-if="activity.location">
                <AppIcon name="map-marker" />
                {{ activity.location }}
              </span>
            </p>
          </div>
          <span class="lr-activity__count">
            {{ t('features.leaderRoster.attendeesCount', attendeesCount(activity)) }}
          </span>
        </header>

        <section v-for="role in activity.roles" :key="role.id" class="lr-role">
          <h3 class="lr-role__title">
            <span>{{ role.name }}</span>
            <span class="lr-role__count">
              {{
                t('features.leaderRoster.peopleCount', role.users.length + role.dependents.length)
              }}
            </span>
          </h3>

          <div v-if="role.users.length" class="lr-group lr-group--users">
            <p class="lr-group__label">
              <AppIcon name="user" />
              <span class="lr-group__name">{{ t('features.leaderRoster.users.title') }}</span>
              <span class="lr-group__hint">{{ t('features.leaderRoster.users.hint') }}</span>
            </p>
            <div class="lr-head lr-cols lr-cols--users" aria-hidden="true">
              <span>{{ t('features.leaderRoster.columns.firstName') }}</span>
              <span>{{ t('features.leaderRoster.columns.lastName') }}</span>
              <span>{{ t('features.leaderRoster.columns.age') }}</span>
              <span>{{ t('features.leaderRoster.columns.email') }}</span>
              <span>{{ t('features.leaderRoster.columns.phone') }}</span>
              <span>{{ t('features.leaderRoster.columns.signedUpAt') }}</span>
            </div>
            <ul class="lr-list">
              <li v-for="(user, index) in role.users" :key="index" class="lr-person">
                <dl class="lr-cols lr-cols--users">
                  <div class="lr-field lr-field--name">
                    <dt>{{ t('features.leaderRoster.columns.firstName') }}</dt>
                    <dd>{{ user.firstName }}</dd>
                  </div>
                  <div class="lr-field lr-field--name">
                    <dt>{{ t('features.leaderRoster.columns.lastName') }}</dt>
                    <dd>{{ user.lastName }}</dd>
                  </div>
                  <div class="lr-field">
                    <dt>{{ t('features.leaderRoster.columns.age') }}</dt>
                    <dd>
                      <span class="lr-tag">{{ t('features.leaderRoster.adult') }}</span>
                    </dd>
                  </div>
                  <div class="lr-field lr-field--contact">
                    <dt>{{ t('features.leaderRoster.columns.email') }}</dt>
                    <dd>
                      <a v-if="user.email" :href="mailHref(user.email)">{{ user.email }}</a>
                      <template v-else>—</template>
                    </dd>
                  </div>
                  <div class="lr-field lr-field--contact">
                    <dt>{{ t('features.leaderRoster.columns.phone') }}</dt>
                    <dd>
                      <a v-if="user.phone" :href="telHref(user.phone)">{{ user.phone }}</a>
                      <template v-else>—</template>
                    </dd>
                  </div>
                  <div class="lr-field lr-field--date">
                    <dt>{{ t('features.leaderRoster.columns.signedUpAt') }}</dt>
                    <dd>
                      <time :datetime="user.signedUpAt" :title="formatDateTime(user.signedUpAt)">
                        {{ formatDate(user.signedUpAt) }}
                      </time>
                    </dd>
                  </div>
                </dl>
              </li>
            </ul>
          </div>

          <div v-if="role.dependents.length" class="lr-group lr-group--dependents">
            <p class="lr-group__label">
              <AppIcon name="id-card" />
              <span class="lr-group__name">{{ t('features.leaderRoster.dependents.title') }}</span>
              <span class="lr-group__hint">{{ t('features.leaderRoster.dependents.hint') }}</span>
            </p>
            <div class="lr-head lr-cols lr-cols--dependents" aria-hidden="true">
              <span>{{ t('features.leaderRoster.columns.firstName') }}</span>
              <span>{{ t('features.leaderRoster.columns.lastName') }}</span>
              <span>{{ t('features.leaderRoster.columns.age') }}</span>
              <span>{{ t('features.leaderRoster.columns.guardian') }}</span>
              <span>{{ t('features.leaderRoster.columns.guardianEmail') }}</span>
              <span>{{ t('features.leaderRoster.columns.guardianPhone') }}</span>
              <span>{{ t('features.leaderRoster.columns.signedUpAt') }}</span>
            </div>
            <ul class="lr-list">
              <li v-for="(dependent, index) in role.dependents" :key="index" class="lr-person">
                <dl class="lr-cols lr-cols--dependents">
                  <div class="lr-field lr-field--name">
                    <dt>{{ t('features.leaderRoster.columns.firstName') }}</dt>
                    <dd>{{ dependent.firstName }}</dd>
                  </div>
                  <div class="lr-field lr-field--name">
                    <dt>{{ t('features.leaderRoster.columns.lastName') }}</dt>
                    <dd>{{ dependent.lastName }}</dd>
                  </div>
                  <div class="lr-field">
                    <dt>{{ t('features.leaderRoster.columns.age') }}</dt>
                    <dd>
                      <span class="lr-tag lr-tag--age">{{ ageLabel(dependent) }}</span>
                    </dd>
                  </div>
                  <div class="lr-field">
                    <dt>{{ t('features.leaderRoster.columns.guardian') }}</dt>
                    <dd>{{ dependent.guardian.firstName }} {{ dependent.guardian.lastName }}</dd>
                  </div>
                  <div class="lr-field lr-field--contact">
                    <dt>{{ t('features.leaderRoster.columns.guardianEmail') }}</dt>
                    <dd>
                      <a v-if="dependent.guardian.email" :href="mailHref(dependent.guardian.email)">
                        {{ dependent.guardian.email }}
                      </a>
                      <template v-else>—</template>
                    </dd>
                  </div>
                  <div class="lr-field lr-field--contact">
                    <dt>{{ t('features.leaderRoster.columns.guardianPhone') }}</dt>
                    <dd>
                      <a v-if="dependent.guardian.phone" :href="telHref(dependent.guardian.phone)">
                        {{ dependent.guardian.phone }}
                      </a>
                      <template v-else>—</template>
                    </dd>
                  </div>
                  <div class="lr-field lr-field--date">
                    <dt>{{ t('features.leaderRoster.columns.signedUpAt') }}</dt>
                    <dd>
                      <time
                        :datetime="dependent.signedUpAt"
                        :title="formatDateTime(dependent.signedUpAt)"
                      >
                        {{ formatDate(dependent.signedUpAt) }}
                      </time>
                    </dd>
                  </div>
                </dl>
              </li>
            </ul>
          </div>
        </section>
      </article>
    </DataState>
  </div>
</template>

<style scoped>
.leader-roster {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.leader-roster__notice {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 14px 16px;
  border: 1px solid var(--ca-info);
  border-radius: 14px;
  background: linear-gradient(var(--ca-info-soft), var(--ca-info-soft)), var(--ca-bg-elevated);
  color: var(--ca-text);
}

.leader-roster__notice .app-icon {
  flex-shrink: 0;
  margin-top: 1px;
  font-size: 20px;
  color: var(--ca-info-ink);
}

.leader-roster__notice-title {
  margin: 0;
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 15px;
  color: var(--ca-text-bright);
}

.leader-roster__notice-text {
  margin: 4px 0 0;
  font-size: 14px;
  line-height: 1.55;
  color: var(--ca-text-muted);
}

.lr-activity {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 22px 24px 24px;
  border: 1px solid var(--ca-border);
  border-radius: 18px;
  background: var(--ca-bg-elevated);
  box-shadow: 0 16px 40px var(--ca-shadow-md);
}

.lr-activity__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--ca-border-soft);
}

.lr-activity__heading {
  min-width: 0;
}

.lr-activity__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 21px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.lr-activity__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 18px;
  margin: 8px 0 0;
  font-family: var(--ca-font-mono);
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.lr-activity__meta span {
  display: inline-flex;
  align-items: center;
  gap: 7px;
}

.lr-activity__count {
  flex-shrink: 0;
  padding: 4px 12px;
  border-radius: 999px;
  background: var(--ca-orange-soft);
  color: var(--ca-orange-ink);
  font-size: 13px;
  font-weight: 600;
}

.lr-role {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.lr-role__title {
  display: flex;
  align-items: baseline;
  gap: 10px;
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 16px;
  color: var(--ca-text-bright);
}

.lr-role__title::before {
  content: '';
  align-self: center;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--ca-orange);
  box-shadow: 0 0 0 3px var(--ca-orange-soft);
}

.lr-role__count {
  font-family: var(--ca-font-body);
  font-size: 13px;
  font-weight: 500;
  color: var(--ca-text-dim);
}

.lr-group {
  --kind: var(--ca-lime);
  --kind-soft: var(--ca-lime-soft);
  --kind-ink: var(--ca-lime-ink);

  container-type: inline-size;
  overflow: hidden;
  border: 1px solid var(--ca-border-soft);
  border-left: 3px solid var(--kind);
  border-radius: 12px;
  background: var(--ca-surface);
}

.lr-group--dependents {
  --kind: var(--ca-azure);
  --kind-soft: var(--ca-azure-soft);
  --kind-ink: var(--ca-azure-ink);
}

.lr-group__label {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px 8px;
  margin: 0;
  padding: 9px 14px;
  background: var(--kind-soft);
  color: var(--kind-ink);
  font-size: 13.5px;
}

.lr-group__name {
  font-weight: 700;
}

.lr-group__hint {
  color: var(--ca-text-muted);
}

.lr-cols {
  display: grid;
  align-items: center;
  gap: 12px;
}

.lr-cols--users {
  grid-template-columns:
    minmax(0, 1fr) minmax(0, 1.3fr) minmax(0, 0.7fr) minmax(0, 2fr) minmax(0, 1.1fr)
    minmax(0, 0.9fr);
}

.lr-cols--dependents {
  grid-template-columns:
    minmax(0, 0.9fr) minmax(0, 1.2fr) minmax(0, 0.6fr) minmax(0, 1.3fr) minmax(0, 2.1fr)
    minmax(0, 1fr) minmax(0, 0.9fr);
}

.lr-head {
  padding: 8px 14px;
  border-bottom: 1px solid var(--ca-border-soft);
  font-size: 11.5px;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--ca-text-dim);
}

.lr-list {
  margin: 0;
  padding: 0;
  list-style: none;
}

.lr-person {
  padding: 10px 14px;
  font-size: 14px;
  color: var(--ca-text);
}

.lr-person + .lr-person {
  border-top: 1px solid var(--ca-border-soft);
}

.lr-person dl {
  margin: 0;
}

.lr-field {
  min-width: 0;
}

.lr-field dt {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

.lr-field dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.lr-field--name dd {
  font-weight: 600;
  color: var(--ca-text-bright);
}

.lr-field--contact a {
  color: var(--ca-text);
  text-decoration: none;
  border-bottom: 1px dashed var(--ca-border-strong);
}

.lr-field--contact a:hover {
  color: var(--ca-text-bright);
  border-bottom-color: var(--kind);
}

.lr-field--date dd {
  font-family: var(--ca-font-mono);
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.lr-tag {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 6px;
  background: var(--ca-surface-2);
  color: var(--ca-text-muted);
  font-size: 12.5px;
  font-weight: 600;
  white-space: nowrap;
}

.lr-tag--age {
  background: var(--kind-soft);
  color: var(--kind-ink);
}

@media (max-width: 860px) {
  .lr-activity {
    padding: 18px 16px 20px;
  }
}

@container (max-width: 880px) {
  .lr-head {
    display: none;
  }

  .lr-cols--users,
  .lr-cols--dependents {
    grid-template-columns: minmax(0, 1fr);
    gap: 6px;
  }

  .lr-person {
    padding: 12px 14px;
  }

  .lr-field {
    display: grid;
    grid-template-columns: minmax(0, 7.5rem) minmax(0, 1fr);
    align-items: baseline;
    gap: 10px;
  }

  .lr-field dt {
    position: static;
    width: auto;
    height: auto;
    overflow: visible;
    clip-path: none;
    white-space: normal;
    font-size: 12.5px;
    color: var(--ca-text-dim);
  }
}

@container (min-width: 560px) and (max-width: 880px) {
  .lr-cols--users,
  .lr-cols--dependents {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    column-gap: 28px;
  }
}
</style>
