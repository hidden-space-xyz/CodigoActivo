<script setup lang="ts">
import { useForgotPassword } from '../model/use-forgot-password'
import { EMAIL_ADDRESS_MAX_LENGTH, ONE_TIME_CODE_LIFETIME_MINUTES } from '@/shared/config'
import { BrandButton } from '@/shared/ui/brand-button'
import { PageHead } from '@/shared/ui/page-head'
import { StatusPanel } from '@/shared/ui/status-panel'

const { form, sent, submit, isSubmitting, isError } = useForgotPassword()
</script>

<template>
  <div>
    <PageHead
      :eyebrow="$t('pages.forgotPassword.eyebrow')"
      :title="$t('pages.forgotPassword.title')"
    >
      <p class="forgot-head__intro">
        {{ $t('pages.forgotPassword.intro') }}
      </p>
    </PageHead>

    <section class="forgot-body">
      <div class="forgot-card">
        <form v-if="!sent" class="forgot-form" @submit.prevent="submit">
          <div class="ca-field">
            <label class="ca-field__label" for="forgot-email">{{ $t('common.emailLong') }}</label>
            <el-input
              id="forgot-email"
              v-model="form.email"
              type="email"
              inputmode="email"
              autocomplete="email"
              autocapitalize="none"
              autocorrect="off"
              spellcheck="false"
              :maxlength="EMAIL_ADDRESS_MAX_LENGTH"
              required
            />
          </div>

          <p v-if="isError" class="ca-form-alert" role="alert">
            {{ $t('pages.forgotPassword.error') }}
          </p>

          <BrandButton type="submit" variant="primary" block :loading="isSubmitting">
            {{ $t('pages.forgotPassword.submit') }}
          </BrandButton>
        </form>

        <StatusPanel
          v-else
          state="success"
          aria-live="polite"
          :title="$t('pages.forgotPassword.sentTitle')"
          :text="$t('pages.forgotPassword.sentBody')"
          :hint="$t('pages.forgotPassword.sentExpiry', { minutes: ONE_TIME_CODE_LIFETIME_MINUTES })"
        />

        <p class="forgot-alt">
          {{ $t('pages.forgotPassword.rememberedAlt') }}
          <RouterLink :to="{ name: 'login' }" class="forgot-alt__link">
            {{ $t('common.login') }}
          </RouterLink>
        </p>
      </div>
    </section>
  </div>
</template>

<style scoped>
.forgot-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 520px;
}

.forgot-body {
  padding: 24px var(--ca-gutter) 80px;
}

.forgot-card {
  max-width: 440px;
  margin: 0 auto;
}

.forgot-form {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.forgot-alt {
  text-align: center;
  margin-top: 18px;
  font-size: 14.5px;
  color: var(--ca-text-muted);
}

.forgot-alt__link {
  color: var(--ca-orange-ink);
  font-weight: 600;
  text-decoration: none;
}

.forgot-alt__link:hover {
  text-decoration: underline;
}
</style>
