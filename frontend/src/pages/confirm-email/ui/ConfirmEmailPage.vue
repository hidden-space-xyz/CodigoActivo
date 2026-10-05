<script setup lang="ts">
import { onMounted } from 'vue'

import { useEmailChangeConfirmation } from '../model/use-email-change-confirmation'
import { useSession } from '@/entities/session'
import { useLinkFragment } from '@/shared/lib/navigation'
import { BrandButton } from '@/shared/ui/brand-button'
import { PageHead } from '@/shared/ui/page-head'
import { StatusPanel } from '@/shared/ui/status-panel'

const linkParam = useLinkFragment()
const session = useSession()
const { state, errorMessage, confirm } = useEmailChangeConfirmation()

onMounted(() => {
  confirm(linkParam('userId'), linkParam('code'))
})
</script>

<template>
  <div>
    <PageHead :eyebrow="$t('pages.confirmEmail.eyebrow')" :title="$t('pages.confirmEmail.title')" />

    <section class="confirm-body">
      <div class="ca-container--narrow">
        <StatusPanel
          v-if="state === 'confirming'"
          state="pending"
          :text="$t('pages.confirmEmail.confirming')"
        />

        <StatusPanel
          v-else-if="state === 'success'"
          state="success"
          :title="$t('pages.confirmEmail.successTitle')"
          :text="$t('pages.confirmEmail.successText')"
        >
          <template #actions>
            <BrandButton v-if="session.isAuthenticated" :to="{ name: 'account' }" variant="primary">
              {{ $t('common.myAccount') }}
            </BrandButton>
            <BrandButton v-else :to="{ name: 'login' }" variant="primary">
              {{ $t('common.login') }}
            </BrandButton>
          </template>
        </StatusPanel>

        <StatusPanel
          v-else
          state="error"
          :title="$t('pages.confirmEmail.errorTitle')"
          :text="errorMessage"
          :hint="$t('pages.confirmEmail.hint')"
        >
          <template #actions>
            <BrandButton :to="{ name: 'home' }" variant="ghost">{{
              $t('common.backToHome')
            }}</BrandButton>
          </template>
        </StatusPanel>
      </div>
    </section>
  </div>
</template>

<style scoped>
.confirm-body {
  padding: 24px var(--ca-gutter) 80px;
}
</style>
