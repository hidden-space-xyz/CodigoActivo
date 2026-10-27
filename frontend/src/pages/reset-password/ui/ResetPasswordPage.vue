<script setup lang="ts">
import { useResetPassword } from '../model/use-reset-password'
import { PASSWORD_MAX_LENGTH } from '@/shared/config'
import { useLinkFragment } from '@/shared/lib/navigation'
import { BrandButton } from '@/shared/ui/brand-button'
import { PageHead } from '@/shared/ui/page-head'
import { StatusPanel } from '@/shared/ui/status-panel'

const linkParam = useLinkFragment()

const { form, state, errorMessage, canRequestNewLink, submit, hasValidLink, isSubmitting } =
  useResetPassword(linkParam('userId'), linkParam('code'))
</script>

<template>
  <div>
    <PageHead
      :eyebrow="$t('pages.resetPassword.eyebrow')"
      :title="$t('pages.resetPassword.title')"
    />

    <section class="reset-body">
      <div class="reset-card">
        <StatusPanel
          v-if="!hasValidLink"
          state="error"
          :title="$t('pages.resetPassword.invalidTitle')"
          :text="$t('pages.resetPassword.invalidText')"
        >
          <template #actions>
            <BrandButton :to="{ name: 'forgot-password' }" variant="primary">
              {{ $t('pages.resetPassword.requestNewLink') }}
            </BrandButton>
            <BrandButton :to="{ name: 'home' }" variant="ghost">{{
              $t('common.backToHome')
            }}</BrandButton>
          </template>
        </StatusPanel>

        <StatusPanel
          v-else-if="state === 'success'"
          state="success"
          :title="$t('pages.resetPassword.successTitle')"
          :text="$t('pages.resetPassword.successText')"
        >
          <template #actions>
            <BrandButton :to="{ name: 'login' }" variant="primary">{{
              $t('common.login')
            }}</BrandButton>
          </template>
        </StatusPanel>

        <form v-else class="reset-form" @submit.prevent="submit">
          <div class="ca-field">
            <label class="ca-field__label" for="reset-password">{{
              $t('pages.resetPassword.newPasswordLabel')
            }}</label>
            <el-input
              id="reset-password"
              v-model="form.password"
              type="password"
              show-password
              autocomplete="new-password"
              :maxlength="PASSWORD_MAX_LENGTH"
              required
            />
          </div>
          <div class="ca-field">
            <label class="ca-field__label" for="reset-password-confirm">{{
              $t('pages.resetPassword.confirmPasswordLabel')
            }}</label>
            <el-input
              id="reset-password-confirm"
              v-model="form.confirmPassword"
              type="password"
              show-password
              autocomplete="new-password"
              :maxlength="PASSWORD_MAX_LENGTH"
              required
            />
          </div>

          <p v-if="errorMessage" class="ca-form-alert" role="alert">{{ errorMessage }}</p>
          <RouterLink
            v-if="canRequestNewLink"
            :to="{ name: 'forgot-password' }"
            class="reset-request-link"
          >
            {{ $t('pages.resetPassword.requestNewLink') }}
          </RouterLink>

          <BrandButton type="submit" variant="primary" block :loading="isSubmitting">
            {{ $t('pages.resetPassword.submit') }}
          </BrandButton>
        </form>
      </div>
    </section>
  </div>
</template>

<style scoped>
.reset-body {
  padding: 24px var(--ca-gutter) 80px;
}

.reset-card {
  max-width: 440px;
  margin: 0 auto;
}

.reset-form {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.reset-request-link {
  margin-top: -8px;
  font-size: 13.5px;
  font-weight: 600;
  color: var(--ca-orange-ink);
  text-decoration: none;
}

.reset-request-link:hover {
  text-decoration: underline;
}
</style>
