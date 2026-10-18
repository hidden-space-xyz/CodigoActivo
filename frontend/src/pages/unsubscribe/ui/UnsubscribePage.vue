<script setup lang="ts">
import { onMounted } from 'vue'

import { useUnsubscribe } from '../model/use-unsubscribe'
import { CONTACT } from '@/shared/config'
import { useLinkFragment } from '@/shared/lib/navigation'
import { BrandButton } from '@/shared/ui/brand-button'
import { PageHead } from '@/shared/ui/page-head'
import { StatusPanel } from '@/shared/ui/status-panel'

const linkParam = useLinkFragment()
const { state, errorMessage, unsubscribe } = useUnsubscribe()

onMounted(() => {
  unsubscribe(linkParam('token'))
})
</script>

<template>
  <div>
    <PageHead :eyebrow="$t('pages.unsubscribe.eyebrow')" :title="$t('pages.unsubscribe.title')" />

    <section class="unsubscribe-body">
      <div class="ca-container--narrow">
        <StatusPanel
          v-if="state === 'unsubscribing'"
          state="pending"
          :text="$t('pages.unsubscribe.unsubscribing')"
        />

        <StatusPanel
          v-else-if="state === 'success'"
          state="success"
          :title="$t('pages.unsubscribe.successTitle')"
          :text="$t('pages.unsubscribe.successText')"
        >
          <template #actions>
            <BrandButton :to="{ name: 'home' }" variant="primary">
              {{ $t('common.backToHome') }}
            </BrandButton>
          </template>
        </StatusPanel>

        <StatusPanel
          v-else
          state="error"
          :title="$t('pages.unsubscribe.errorTitle')"
          :text="errorMessage"
          :hint="$t('pages.unsubscribe.hint', { email: CONTACT.email })"
        >
          <template #actions>
            <BrandButton :to="{ name: 'home' }" variant="ghost">
              {{ $t('common.backToHome') }}
            </BrandButton>
          </template>
        </StatusPanel>
      </div>
    </section>
  </div>
</template>

<style scoped>
.unsubscribe-body {
  padding: 24px var(--ca-gutter) 80px;
}
</style>
