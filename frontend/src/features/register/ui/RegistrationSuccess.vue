<script setup lang="ts">
import { BaseButton } from '@/shared/ui'

const props = defineProps<{
  minorCount: number
  email: string
  requiresVerification: boolean
  isResending: boolean
  resendCooldown: number
}>()
const emit = defineEmits<{ reset: []; resend: [] }>()
</script>

<template>
  <div class="reg-success">
    <div class="reg-success__check" aria-hidden="true">✓</div>
    <h2 class="reg-success__title">{{ $t('features.register.success.title') }}</h2>
    <p v-if="minorCount > 0" class="reg-success__role">
      {{ $t('features.register.success.minorsEnrolledBefore') }}
      <b>{{ $t('features.register.success.minorsEnrolled', { n: minorCount }, minorCount) }}</b>
      {{ $t('features.register.success.minorsEnrolledAfter') }}
    </p>

    <p class="reg-success__thanks">
      {{ $t('features.register.success.thanks') }}
    </p>
    <p class="reg-success__reminder">
      {{ $t('features.register.success.reminder') }}
    </p>

    <div v-if="!props.requiresVerification" class="reg-success__verified">
      {{ $t('features.register.success.verifiedActive') }}
      <BaseButton :to="{ name: 'login' }" variant="primary" class="reg-success__login">
        {{ $t('common.login') }}
      </BaseButton>
    </div>

    <div v-else class="reg-success__verify">
      <p class="reg-success__verify-intro">
        {{ $t('features.register.success.verifyIntroBefore') }} <b>{{ email }}</b
        >{{ $t('features.register.success.verifyIntroAfter') }}
      </p>
      <p class="reg-success__resend">
        {{ $t('features.register.success.resendPrompt') }}
        <BaseButton
          variant="link"
          class="reg-success__resend-button"
          :disabled="resendCooldown > 0 || isResending"
          :loading="isResending"
          @click="emit('resend')"
        >
          {{
            resendCooldown > 0
              ? $t('features.register.success.resendCountdown', { s: resendCooldown })
              : $t('features.register.success.resend')
          }}
        </BaseButton>
      </p>
    </div>

    <div class="reg-success__actions">
      <BaseButton :to="{ name: 'home' }" variant="primary">
        {{ $t('common.backToHome') }}
      </BaseButton>
      <BaseButton :to="{ name: 'events' }" variant="ghost">
        {{ $t('features.register.success.viewEvents') }}
      </BaseButton>
    </div>

    <BaseButton variant="link" class="reg-success__again" @click="emit('reset')">
      {{ $t('features.register.success.registerAnother') }}
    </BaseButton>
  </div>
</template>

<style scoped>
.reg-success {
  max-width: 540px;
  margin: 0 auto;
  text-align: center;
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 20px;
  padding: 44px 36px;
}

.reg-success__check {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: var(--ca-success-soft);
  border: 1px solid var(--ca-success);
  color: var(--ca-success);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  margin: 0 auto 20px;
}

.reg-success__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 26px;
  color: var(--ca-text-bright);
}

.reg-success__role {
  margin-top: 8px;
  font-size: 15px;
  color: var(--ca-text-muted);
}

.reg-success__role b {
  color: var(--ca-text);
}

.reg-success__thanks {
  margin-top: 18px;
  font-size: 16px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.reg-success__reminder {
  margin-top: 12px;
  font-size: 15px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.reg-success__actions {
  display: flex;
  gap: 14px;
  justify-content: center;
  margin-top: 28px;
  flex-wrap: wrap;
}

.reg-success__again {
  margin-top: 18px;
}

.reg-success__verify {
  margin-top: 22px;
  text-align: left;
  background: var(--ca-orange-soft);
  border: 1px solid var(--ca-border-strong);
  border-radius: 12px;
  padding: 18px;
}

.reg-success__verify-intro {
  font-size: 14.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  margin-bottom: 14px;
}

.reg-success__verify-intro b {
  color: var(--ca-text);
  overflow-wrap: anywhere;
}

.reg-success__resend {
  margin-top: 12px;
  font-size: 13.5px;
  color: var(--ca-text-muted);
}

.reg-success__resend-button {
  font-size: 13.5px;
}

.reg-success__verified {
  margin-top: 22px;
  background: var(--ca-success-soft);
  border: 1px solid var(--ca-success);
  border-radius: 12px;
  padding: 14px;
  color: var(--ca-text);
  font-size: 14.5px;
}

.reg-success__login {
  display: block;
  margin: 12px auto 0;
  width: fit-content;
}
</style>
