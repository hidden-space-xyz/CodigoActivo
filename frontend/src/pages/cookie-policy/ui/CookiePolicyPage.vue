<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query'

import { LegalPolicyDocument, legalPolicyQueries } from '@/entities/legal-policy'

const { data: policy, isLoading, isError } = useQuery(legalPolicyQueries.current('Cookies'))
</script>

<template>
  <LegalPolicyDocument
    :eyebrow="$t('pages.cookiePolicy.eyebrow')"
    :title="$t('pages.cookiePolicy.title')"
    :policy="policy"
    :loading="isLoading"
    :error="isError"
  />
</template>
