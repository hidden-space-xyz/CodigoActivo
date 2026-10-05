<script setup lang="ts">
import { onMounted } from 'vue'

import { useAccountVerification } from '../model/use-account-verification'
import { useLinkFragment } from '@/shared/lib/navigation'
import { BrandButton } from '@/shared/ui/brand-button'
import { PageHead } from '@/shared/ui/page-head'
import { StatusPanel } from '@/shared/ui/status-panel'

const linkParam = useLinkFragment()
const { state, errorMessage, verify, resendForm, resend, isResending } = useAccountVerification()

onMounted(() => {
  verify(linkParam('userId'), linkParam('code'))
})
</script>

<template>
  <div>
    <PageHead
      :eyebrow="$t('pages.verifyAccount.eyebrow')"
      :title="$t('pages.verifyAccount.title')"
    />

    <section class="verify-body">
      <div class="ca-container--narrow">
        <StatusPanel
          v-if="state === 'verifying'"
          state="pending"
          :text="$t('pages.verifyAccount.verifying')"
        />

        <StatusPanel
          v-else-if="state === 'success'"
          state="success"
          :title="$t('pages.verifyAccount.successTitle')"
          :text="$t('pages.verifyAccount.successText')"
        >
          <template #actions>
            <BrandButton :to="{ name: 'login' }" variant="primary">{{
              $t('common.login')
            }}</BrandButton>
          </template>
        </StatusPanel>

        <StatusPanel
          v-else
          state="error"
          :title="$t('pages.verifyAccount.errorTitle')"
          :text="errorMessage ?? $t('pages.verifyAccount.defaultError')"
          :hint="$t('pages.verifyAccount.hint')"
        >
          <form class="verify-resend" @submit.prevent="resend">
            <label class="verify-resend__label" for="verify-resend-email">{{
              $t('common.emailLong')
            }}</label>
            <el-input
              id="verify-resend-email"
              v-model="resendForm.email"
              type="email"
              inputmode="email"
              autocomplete="email"
              autocapitalize="none"
              autocorrect="off"
              spellcheck="false"
              :maxlength="256"
              required
            />
            <BrandButton type="submit" variant="primary" block :loading="isResending">
              {{ $t('pages.verifyAccount.resend') }}
            </BrandButton>
          </form>
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
.verify-body {
  padding: 24px var(--ca-gutter) 80px;
}

.verify-resend {
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-width: 360px;
  margin: 20px auto 0;
  text-align: left;
}

.verify-resend__label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}
</style>
