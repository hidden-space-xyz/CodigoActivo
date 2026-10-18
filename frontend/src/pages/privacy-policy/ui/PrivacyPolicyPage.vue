<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useQuery } from '@tanstack/vue-query'

import { legalEntityQueries } from '@/entities/legal-entity'
import { LegalDocument, LegalEntry, LegalEntryList, LegalSection } from '@/shared/ui/legal-document'

import { AUTHORITY_URL, PROCESSING_ACTIVITIES, type ProcessingActivity } from '../config/processing'

const { t } = useI18n()

const { data: legalEntity } = useQuery(legalEntityQueries.detail())

const controller = computed(() => {
  const identity = legalEntity.value
  return [
    { label: t('pages.privacyPolicy.controller.name'), value: identity?.name || '—' },
    { label: t('pages.privacyPolicy.controller.taxId'), value: identity?.taxId || '—' },
    { label: t('pages.privacyPolicy.controller.address'), value: identity?.address || '—' },
    { label: t('pages.privacyPolicy.controller.registry'), value: identity?.registry || '—' },
    { label: t('pages.privacyPolicy.controller.email'), value: identity?.email || '—' },
    { label: t('pages.privacyPolicy.controller.phone'), value: identity?.phone?.text || '—' },
  ]
})

function facts(activity: ProcessingActivity) {
  return [
    { label: t('pages.privacyPolicy.processing.labels.data'), value: t(activity.dataKey) },
    { label: t('pages.privacyPolicy.processing.labels.purpose'), value: t(activity.purposeKey) },
    {
      label: t('pages.privacyPolicy.processing.labels.legalBasis'),
      value: t(activity.legalBasisKey),
    },
    {
      label: t('pages.privacyPolicy.processing.labels.retention'),
      value: t(activity.retentionKey),
    },
  ]
}
</script>

<template>
  <LegalDocument
    :eyebrow="$t('pages.privacyPolicy.eyebrow')"
    :title="$t('pages.privacyPolicy.title')"
    :lead="$t('pages.privacyPolicy.lead')"
    :updated="$t('pages.privacyPolicy.updated')"
  >
    <LegalSection id="privacy-controller" :title="$t('pages.privacyPolicy.controller.title')">
      <LegalEntryList>
        <LegalEntry :facts="controller" />
      </LegalEntryList>
      <i18n-t keypath="pages.privacyPolicy.controller.dpo" tag="p">
        <template #email>
          <a v-if="legalEntity?.email" :href="`mailto:${legalEntity.email}`">{{
            legalEntity.email
          }}</a>
          <template v-else>—</template>
        </template>
      </i18n-t>
    </LegalSection>

    <LegalSection id="privacy-processing" :title="$t('pages.privacyPolicy.processing.title')">
      <p>{{ $t('pages.privacyPolicy.processing.text') }}</p>
      <LegalEntryList>
        <LegalEntry
          v-for="activity in PROCESSING_ACTIVITIES"
          :key="activity.id"
          :facts="facts(activity)"
        >
          <template #head>
            <h3 class="privacy-policy__activity-title">{{ $t(activity.titleKey) }}</h3>
          </template>
        </LegalEntry>
      </LegalEntryList>
    </LegalSection>

    <LegalSection id="privacy-minors" :title="$t('pages.privacyPolicy.minors.title')">
      <p>{{ $t('pages.privacyPolicy.minors.text') }}</p>
      <p>{{ $t('pages.privacyPolicy.minors.rights') }}</p>
    </LegalSection>

    <LegalSection id="privacy-required" :title="$t('pages.privacyPolicy.required.title')">
      <p>{{ $t('pages.privacyPolicy.required.text') }}</p>
    </LegalSection>

    <LegalSection id="privacy-recipients" :title="$t('pages.privacyPolicy.recipients.title')">
      <p>{{ $t('pages.privacyPolicy.recipients.internal') }}</p>
      <p>{{ $t('pages.privacyPolicy.recipients.processors') }}</p>
      <ul>
        <li>{{ $t('pages.privacyPolicy.recipients.hosting') }}</li>
        <li>{{ $t('pages.privacyPolicy.recipients.email') }}</li>
      </ul>
      <p>{{ $t('pages.privacyPolicy.recipients.disclosures') }}</p>
    </LegalSection>

    <LegalSection id="privacy-transfers" :title="$t('pages.privacyPolicy.transfers.title')">
      <p>{{ $t('pages.privacyPolicy.transfers.text') }}</p>
    </LegalSection>

    <LegalSection id="privacy-automated" :title="$t('pages.privacyPolicy.automated.title')">
      <p>{{ $t('pages.privacyPolicy.automated.text') }}</p>
    </LegalSection>

    <LegalSection id="privacy-rights" :title="$t('pages.privacyPolicy.rights.title')">
      <p>{{ $t('pages.privacyPolicy.rights.list') }}</p>
      <p>
        <strong>{{ $t('pages.privacyPolicy.rights.objection') }}</strong>
      </p>
      <i18n-t keypath="pages.privacyPolicy.rights.self" tag="p">
        <template #account>
          <RouterLink :to="{ name: 'account' }">{{
            $t('pages.privacyPolicy.rights.account')
          }}</RouterLink>
        </template>
      </i18n-t>
      <i18n-t keypath="pages.privacyPolicy.rights.how" tag="p">
        <template #email>
          <a v-if="legalEntity?.email" :href="`mailto:${legalEntity.email}`">{{
            legalEntity.email
          }}</a>
          <template v-else>—</template>
        </template>
      </i18n-t>
      <p>{{ $t('pages.privacyPolicy.rights.deceased') }}</p>
      <i18n-t keypath="pages.privacyPolicy.rights.complaint" tag="p">
        <template #authority>
          <a :href="AUTHORITY_URL" target="_blank" rel="noopener">{{
            $t('pages.privacyPolicy.rights.authority')
          }}</a>
        </template>
      </i18n-t>
    </LegalSection>

    <LegalSection id="privacy-security" :title="$t('pages.privacyPolicy.security.title')">
      <p>{{ $t('pages.privacyPolicy.security.text') }}</p>
    </LegalSection>

    <LegalSection id="privacy-cookies" :title="$t('pages.privacyPolicy.cookies.title')">
      <i18n-t keypath="pages.privacyPolicy.cookies.text" tag="p">
        <template #policy>
          <RouterLink :to="{ name: 'cookie-policy' }">{{
            $t('pages.privacyPolicy.cookies.policy')
          }}</RouterLink>
        </template>
      </i18n-t>
    </LegalSection>

    <LegalSection id="privacy-changes" :title="$t('pages.privacyPolicy.changes.title')">
      <p>{{ $t('pages.privacyPolicy.changes.text') }}</p>
    </LegalSection>
  </LegalDocument>
</template>

<style scoped>
.privacy-policy__activity-title {
  font-family: var(--ca-font-display);
  font-size: 18px;
  font-weight: 700;
  line-height: 1.3;
  color: var(--ca-text-bright);
}
</style>
