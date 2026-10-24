<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useQuery } from '@tanstack/vue-query'

import { legalEntityQueries } from '@/entities/legal-entity'
import { LegalPolicyDocument, legalPolicyQueries } from '@/entities/legal-policy'
import { LegalEntry, LegalEntryList, LegalSection } from '@/shared/ui/legal-document'

const { t } = useI18n()

const { data: legalEntity } = useQuery(legalEntityQueries.detail())
const { data: policy, isLoading, isError } = useQuery(legalPolicyQueries.current('Privacy'))

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
</script>

<template>
  <LegalPolicyDocument
    :eyebrow="$t('pages.privacyPolicy.eyebrow')"
    :title="$t('pages.privacyPolicy.title')"
    :policy="policy"
    :loading="isLoading"
    :error="isError"
  >
    <LegalSection id="privacy-controller" :title="$t('pages.privacyPolicy.controller.title')">
      <LegalEntryList>
        <LegalEntry :facts="controller" />
      </LegalEntryList>
    </LegalSection>
  </LegalPolicyDocument>
</template>
