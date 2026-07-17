<script setup lang="ts">
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'

import { useLogin } from '@/features/auth'
import { BaseButton, SectionEyebrow } from '@/shared/ui'

const { form, submit, isSubmitting, isError } = useLogin()
</script>

<template>
  <div>
    <section class="login-head">
      <div class="login-head__glow" aria-hidden="true" />
      <div class="ca-container--narrow login-head__inner">
        <SectionEyebrow :text="$t('pages.login.eyebrow')" color="var(--ca-orange-ink)" />
        <h1 class="login-head__title">{{ $t('pages.login.title') }}</h1>
        <p class="login-head__intro">
          {{ $t('pages.login.intro') }}
        </p>
      </div>
    </section>

    <section class="login-body">
      <div class="login-card">
        <form class="login-form" @submit.prevent="submit">
          <div class="login-field">
            <label class="login-label" for="login-identifier">{{ $t('pages.login.identifierLabel') }}</label>
            <InputText id="login-identifier" v-model="form.identifier" required fluid />
          </div>
          <div class="login-field">
            <label class="login-label" for="login-password">{{ $t('common.password') }}</label>
            <Password
              input-id="login-password"
              v-model="form.password"
              :feedback="false"
              toggle-mask
              required
              fluid
            />
          </div>

          <RouterLink :to="{ name: 'forgot-password' }" class="login-forgot">
            {{ $t('pages.login.forgotLink') }}
          </RouterLink>

          <p v-if="isError" class="login-error" role="alert">
            {{ $t('pages.login.error') }}
          </p>

          <BaseButton type="submit" variant="primary" block :loading="isSubmitting">
            {{ $t('common.login') }}
          </BaseButton>
        </form>

        <p class="login-alt">
          {{ $t('pages.login.noAccount') }}
          <RouterLink :to="{ name: 'register' }" class="login-alt__link"> {{ $t('common.register') }} </RouterLink>
        </p>
      </div>
    </section>
  </div>
</template>

<style scoped>
.login-head {
  position: relative;
  overflow: hidden;
  padding: 64px 24px 16px;
}

.login-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-orange-soft), transparent 60%);
}

.login-head__inner {
  position: relative;
}

.login-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.login-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 520px;
}

.login-body {
  padding: 24px 24px 80px;
}

.login-card {
  max-width: 440px;
  margin: 0 auto;
}

.login-form {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.login-field {
  display: flex;
  flex-direction: column;
}

.login-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
  margin-bottom: 6px;
}

.login-forgot {
  margin-top: -6px;
  align-self: flex-end;
  font-size: 13.5px;
  font-weight: 600;
  color: var(--ca-orange-ink);
  text-decoration: none;
}

.login-forgot:hover {
  text-decoration: underline;
}

.login-error {
  font-size: 13.5px;
  color: var(--ca-danger-ink);
}

.login-alt {
  text-align: center;
  margin-top: 18px;
  font-size: 14.5px;
  color: var(--ca-text-muted);
}

.login-alt__link {
  color: var(--ca-orange-ink);
  font-weight: 600;
  text-decoration: none;
}

.login-alt__link:hover {
  text-decoration: underline;
}
</style>
