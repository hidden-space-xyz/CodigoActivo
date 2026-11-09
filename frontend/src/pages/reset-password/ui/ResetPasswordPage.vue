<script setup lang="ts">
import { useResetPassword } from '../model/use-reset-password'
import { PASSWORD_MAX_LENGTH } from '@/shared/config'
import { useLinkFragment } from '@/shared/lib/navigation'
import { BrandButton } from '@/shared/ui/brand-button'
import { FormField } from '@/shared/ui/form-field'
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
          data-testid="reset-password-invalid"
        >
          <template #actions>
            <BrandButton
              :to="{ name: 'forgot-password' }"
              variant="primary"
              data-testid="reset-password-request-link"
            >
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
          data-testid="reset-password-success"
        >
          <template #actions>
            <BrandButton :to="{ name: 'login' }" variant="primary">{{
              $t('common.login')
            }}</BrandButton>
          </template>
        </StatusPanel>

        <form v-else class="reset-form" @submit.prevent="submit">
          <FormField for="reset-password" :label="$t('pages.resetPassword.newPasswordLabel')">
            <el-input
              id="reset-password"
              v-model="form.password"
              data-testid="reset-password-new"
              type="password"
              show-password
              autocomplete="new-password"
              :maxlength="PASSWORD_MAX_LENGTH"
              required
            />
          </FormField>
          <FormField
            for="reset-password-confirm"
            :label="$t('pages.resetPassword.confirmPasswordLabel')"
          >
            <el-input
              id="reset-password-confirm"
              v-model="form.confirmPassword"
              data-testid="reset-password-confirm"
              type="password"
              show-password
              autocomplete="new-password"
              :maxlength="PASSWORD_MAX_LENGTH"
              required
            />
          </FormField>

          <p
            v-if="errorMessage"
            class="ca-form-alert"
            role="alert"
            data-testid="reset-password-error"
          >
            {{ errorMessage }}
          </p>
          <RouterLink
            v-if="canRequestNewLink"
            :to="{ name: 'forgot-password' }"
            class="reset-request-link"
            data-testid="reset-password-new-link"
          >
            {{ $t('pages.resetPassword.requestNewLink') }}
          </RouterLink>

          <BrandButton
            type="submit"
            variant="primary"
            block
            :loading="isSubmitting"
            data-testid="reset-password-submit"
          >
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
