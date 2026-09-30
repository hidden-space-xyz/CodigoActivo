<script setup lang="ts">
import { BrandButton } from '@/shared/ui/brand-button'

defineProps<{
  /** Minors registered with the adult; the enrolled-minors message is hidden when 0. */
  minorCount: number
  /** Address the verification email was sent to. */
  email: string
  /** Shows a loading state on the resend button. */
  isResending: boolean
  /** Seconds until resending is allowed again; 0 enables the button. */
  resendCooldown: number
}>()
const emit = defineEmits<{
  /** Fired when the user starts a new registration. */
  reset: []
  /** Fired when the user asks for another verification email. */
  resend: []
}>()
</script>

<template>
  <div class="reg-success">
    <div class="reg-success__check" aria-hidden="true">✓</div>
    <h2 class="reg-success__title">{{ $t('pages.register.success.title') }}</h2>
    <p v-if="minorCount > 0" class="reg-success__role">
      {{ $t('pages.register.success.minorsEnrolledBefore') }}
      <b>{{ $t('pages.register.success.minorsEnrolled', { n: minorCount }, minorCount) }}</b>
      {{ $t('pages.register.success.minorsEnrolledAfter') }}
    </p>

    <p class="reg-success__thanks">
      {{ $t('pages.register.success.thanks') }}
    </p>
    <p class="reg-success__reminder">
      {{ $t('pages.register.success.reminder') }}
    </p>

    <div class="reg-success__verify">
      <p class="reg-success__verify-intro">
        {{ $t('pages.register.success.verifyIntroBefore') }} <b>{{ email }}</b
        >{{ $t('pages.register.success.verifyIntroAfter') }}
      </p>
      <p class="reg-success__resend">
        {{ $t('pages.register.success.resendPrompt') }}
        <BrandButton
          variant="link"
          class="reg-success__resend-button"
          :disabled="resendCooldown > 0 || isResending"
          :loading="isResending"
          @click="emit('resend')"
        >
          {{
            resendCooldown > 0
              ? $t('pages.register.success.resendCountdown', { s: resendCooldown })
              : $t('pages.register.success.resend')
          }}
        </BrandButton>
      </p>
    </div>

    <div class="reg-success__actions">
      <BrandButton :to="{ name: 'home' }" variant="primary">
        {{ $t('common.backToHome') }}
      </BrandButton>
      <BrandButton :to="{ name: 'events' }" variant="ghost">
        {{ $t('pages.register.success.viewEvents') }}
      </BrandButton>
    </div>

    <BrandButton variant="link" class="reg-success__again" @click="emit('reset')">
      {{ $t('pages.register.success.registerAnother') }}
    </BrandButton>
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
</style>
