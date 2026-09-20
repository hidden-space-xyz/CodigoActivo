<script setup lang="ts">
import { useLogin } from '@/features/auth'
import { BaseButton, PageHeading } from '@/shared/ui'

const { form, submit, isSubmitting, isError } = useLogin()
</script>

<template>
  <div>
    <section class="login-head">
      <div class="ca-container">
        <PageHeading :title="$t('pages.login.title')" :description="$t('pages.login.intro')" />
      </div>
    </section>

    <section class="login-body">
      <div class="login-card">
        <form class="login-form" @submit.prevent="submit">
          <div class="login-field">
            <label class="login-label" for="login-identifier">{{
              $t('pages.login.identifierLabel')
            }}</label>
            <el-input
              id="login-identifier"
              v-model="form.identifier"
              autocomplete="username"
              autocapitalize="none"
              autocorrect="off"
              spellcheck="false"
              required
            />
          </div>
          <div class="login-field">
            <label class="login-label" for="login-password">{{ $t('common.password') }}</label>
            <el-input
              id="login-password"
              v-model="form.password"
              type="password"
              show-password
              autocomplete="current-password"
              required
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
          <RouterLink :to="{ name: 'register' }" class="login-alt__link">
            {{ $t('common.register') }}
          </RouterLink>
        </p>
      </div>
    </section>
  </div>
</template>

<style scoped>
.login-head {
  padding: 48px var(--ca-gutter) 20px;
}

.login-body {
  padding: 24px var(--ca-gutter) 80px;
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
