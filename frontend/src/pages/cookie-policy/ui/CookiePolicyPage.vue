<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import { LegalDocument, LegalEntry, LegalEntryList, LegalSection } from '@/shared/ui/legal-document'

import {
  BROWSER_GUIDES,
  COOKIE_GUIDE_URL,
  STORAGE_ENTRIES,
  type StorageEntry,
} from '../config/storage'

const { t } = useI18n()

function facts(entry: StorageEntry) {
  return [
    { label: t('pages.cookiePolicy.used.purpose'), value: t(entry.purposeKey) },
    { label: t('pages.cookiePolicy.used.duration'), value: t(entry.durationKey) },
  ]
}
</script>

<template>
  <LegalDocument
    :eyebrow="$t('pages.cookiePolicy.eyebrow')"
    :title="$t('pages.cookiePolicy.title')"
    :lead="$t('pages.cookiePolicy.lead')"
    :updated="$t('pages.cookiePolicy.updated')"
  >
    <LegalSection id="cookie-policy-used" :title="$t('pages.cookiePolicy.used.title')">
      <p>{{ $t('pages.cookiePolicy.used.text') }}</p>
      <LegalEntryList>
        <LegalEntry v-for="entry in STORAGE_ENTRIES" :key="entry.name" :facts="facts(entry)">
          <template #head>
            <code class="cookie-policy__name">{{ entry.name }}</code>
            <span class="cookie-policy__kind">{{ $t(entry.kindKey) }}</span>
          </template>
        </LegalEntry>
      </LegalEntryList>
    </LegalSection>

    <LegalSection id="cookie-policy-consent" :title="$t('pages.cookiePolicy.consent.title')">
      <i18n-t keypath="pages.cookiePolicy.consent.exemption" tag="p">
        <template #guide>
          <a :href="COOKIE_GUIDE_URL" target="_blank" rel="noopener">{{
            $t('pages.cookiePolicy.consent.guide')
          }}</a>
        </template>
      </i18n-t>
      <p>{{ $t('pages.cookiePolicy.consent.keepSignedIn') }}</p>
    </LegalSection>

    <LegalSection id="cookie-policy-manage" :title="$t('pages.cookiePolicy.manage.title')">
      <p>{{ $t('pages.cookiePolicy.manage.text') }}</p>
      <ul class="cookie-policy__guides">
        <li v-for="guide in BROWSER_GUIDES" :key="guide.url">
          <a :href="guide.url" target="_blank" rel="noopener">{{ $t(guide.labelKey) }}</a>
        </li>
      </ul>
    </LegalSection>

    <LegalSection id="cookie-policy-privacy" :title="$t('pages.cookiePolicy.privacy.title')">
      <i18n-t keypath="pages.cookiePolicy.privacy.text" tag="p">
        <template #policy>
          <RouterLink :to="{ name: 'privacy-policy' }">{{
            $t('pages.cookiePolicy.privacy.policy')
          }}</RouterLink>
        </template>
      </i18n-t>
    </LegalSection>
  </LegalDocument>
</template>

<style scoped>
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
</style>
