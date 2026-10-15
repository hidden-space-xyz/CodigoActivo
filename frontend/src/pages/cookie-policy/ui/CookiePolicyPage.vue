<script setup lang="ts">
import { PageHead } from '@/shared/ui/page-head'

import { BROWSER_GUIDES, COOKIE_GUIDE_URL, STORAGE_ENTRIES } from '../config/storage'
</script>

<template>
  <div>
    <PageHead :eyebrow="$t('pages.cookiePolicy.eyebrow')" :title="$t('pages.cookiePolicy.title')">
      <p class="cookie-policy__lead">{{ $t('pages.cookiePolicy.lead') }}</p>
      <p class="cookie-policy__updated">{{ $t('pages.cookiePolicy.updated') }}</p>
    </PageHead>

    <div class="cookie-policy">
      <div class="ca-container--narrow">
        <div class="cookie-policy__body">
          <section class="cookie-policy__section" aria-labelledby="cookie-policy-used">
            <h2 id="cookie-policy-used" class="cookie-policy__heading">
              {{ $t('pages.cookiePolicy.used.title') }}
            </h2>
            <p class="cookie-policy__text">{{ $t('pages.cookiePolicy.used.text') }}</p>
            <ul class="cookie-policy__entries">
              <li v-for="entry in STORAGE_ENTRIES" :key="entry.name" class="cookie-policy__entry">
                <div class="cookie-policy__entry-head">
                  <code class="cookie-policy__name">{{ entry.name }}</code>
                  <span class="cookie-policy__kind">{{ $t(entry.kindKey) }}</span>
                </div>
                <dl class="cookie-policy__facts">
                  <dt class="cookie-policy__fact-label">
                    {{ $t('pages.cookiePolicy.used.purpose') }}
                  </dt>
                  <dd class="cookie-policy__fact-value">{{ $t(entry.purposeKey) }}</dd>
                  <dt class="cookie-policy__fact-label">
                    {{ $t('pages.cookiePolicy.used.duration') }}
                  </dt>
                  <dd class="cookie-policy__fact-value">{{ $t(entry.durationKey) }}</dd>
                </dl>
              </li>
            </ul>
          </section>

          <section class="cookie-policy__section" aria-labelledby="cookie-policy-consent">
            <h2 id="cookie-policy-consent" class="cookie-policy__heading">
              {{ $t('pages.cookiePolicy.consent.title') }}
            </h2>
            <i18n-t
              keypath="pages.cookiePolicy.consent.exemption"
              tag="p"
              class="cookie-policy__text"
            >
              <template #guide>
                <a
                  :href="COOKIE_GUIDE_URL"
                  target="_blank"
                  rel="noopener"
                  class="cookie-policy__link"
                  >{{ $t('pages.cookiePolicy.consent.guide') }}</a
                >
              </template>
            </i18n-t>
            <p class="cookie-policy__text">{{ $t('pages.cookiePolicy.consent.keepSignedIn') }}</p>
          </section>

          <section class="cookie-policy__section" aria-labelledby="cookie-policy-manage">
            <h2 id="cookie-policy-manage" class="cookie-policy__heading">
              {{ $t('pages.cookiePolicy.manage.title') }}
            </h2>
            <p class="cookie-policy__text">{{ $t('pages.cookiePolicy.manage.text') }}</p>
            <ul class="cookie-policy__guides">
              <li v-for="guide in BROWSER_GUIDES" :key="guide.url">
                <a :href="guide.url" target="_blank" rel="noopener" class="cookie-policy__link">{{
                  $t(guide.labelKey)
                }}</a>
              </li>
            </ul>
          </section>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.cookie-policy__lead {
  max-width: 680px;
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.cookie-policy__updated {
  margin-top: 12px;
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-dim);
}

.cookie-policy {
  padding: 24px var(--ca-gutter) 80px;
}

.cookie-policy__body {
  max-width: 780px;
  display: flex;
  flex-direction: column;
  gap: 44px;
}

.cookie-policy__heading {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: clamp(22px, 3.4vw, 26px);
  line-height: 1.2;
  letter-spacing: -0.02em;
  color: var(--ca-text-bright);
}

.cookie-policy__text {
  margin-top: 12px;
  font-size: 16px;
  line-height: 1.7;
  color: var(--ca-text-muted);
}

.cookie-policy__link {
  color: var(--ca-orange-ink);
  font-weight: 600;
  text-decoration: none;
  overflow-wrap: anywhere;
}

.cookie-policy__link:hover {
  text-decoration: underline;
}

.cookie-policy__entries {
  list-style: none;
  margin: 20px 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.cookie-policy__entry {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 16px;
  padding: 18px 20px;
}

.cookie-policy__entry-head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px 12px;
}

.cookie-policy__name {
  font-family: var(--ca-font-mono);
  font-size: 14px;
  font-weight: 600;
  color: var(--ca-text-bright);
  overflow-wrap: anywhere;
}

.cookie-policy__kind {
  padding: 3px 10px;
  border-radius: 999px;
  background: var(--ca-orange-soft);
  color: var(--ca-orange-ink);
  font-family: var(--ca-font-mono);
  font-size: 11.5px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.cookie-policy__facts {
  display: grid;
  grid-template-columns: 130px minmax(0, 1fr);
  gap: 8px 16px;
  margin: 14px 0 0;
}

.cookie-policy__fact-label {
  font-size: 13px;
  font-weight: 600;
  line-height: 1.7;
  color: var(--ca-text-dim);
}

.cookie-policy__fact-value {
  margin: 0;
  font-size: 15px;
  line-height: 1.6;
  color: var(--ca-text);
}

.cookie-policy__guides {
  margin: 10px 0 0;
  padding-left: 22px;
  list-style: disc;
  font-size: 16px;
  line-height: 1.9;
  color: var(--ca-text-muted);
}

@media (max-width: 640px) {
  .cookie-policy__facts {
    grid-template-columns: minmax(0, 1fr);
    gap: 2px;
  }

  .cookie-policy__fact-value + .cookie-policy__fact-label {
    margin-top: 10px;
  }

  .cookie-policy__entry {
    padding-inline: 16px;
  }
}
</style>
