<script setup lang="ts">
import { ref, watch } from 'vue'
import InputText from 'primevue/inputtext'

import { BaseButton } from '@/shared/ui'

const props = defineProps<{
  minorCount: number
  email: string
  requiresVerification: boolean
  isVerified: boolean
  isVerifying: boolean
  isResending: boolean
  verifyError: string | null
  resendCooldown: number
  resendCount: number
}>()
const emit = defineEmits<{ reset: []; verify: [otp: string]; resend: [] }>()

const otp = ref('')

watch(
  () => props.resendCount,
  () => {
    otp.value = ''
  },
)

function submitVerify(): void {
  if (otp.value.trim()) emit('verify', otp.value.trim())
}
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

    <div v-if="isVerified || !props.requiresVerification" class="reg-success__verified">
      {{
        props.requiresVerification
          ? $t('features.register.success.verifiedRequired')
          : $t('features.register.success.verifiedActive')
      }}
      <BaseButton :to="{ name: 'login' }" variant="primary" class="reg-success__login">
        {{ $t('common.login') }}
      </BaseButton>
    </div>

    <form v-else class="reg-success__verify" @submit.prevent="submitVerify">
      <p class="reg-success__verify-intro">
        {{ $t('features.register.success.verifyIntroBefore') }} <b>{{ email }}</b
        >{{ $t('features.register.success.verifyIntroAfter') }}
      </p>
      <label class="reg-success__verify-label" for="reg-otp">{{
        $t('features.register.success.otpLabel')
      }}</label>
      <div class="reg-success__verify-row">
        <InputText
          id="reg-otp"
          v-model="otp"
          :placeholder="$t('features.register.success.otpPlaceholder')"
          :invalid="verifyError !== null"
          fluid
        />
        <BaseButton type="submit" variant="primary" :loading="isVerifying" :disabled="!otp.trim()">
          {{ $t('features.register.success.verify') }}
        </BaseButton>
      </div>
      <small v-if="verifyError" class="reg-success__verify-error" role="alert">
        {{ verifyError }}
      </small>
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
    </form>

    <div class="reg-success__actions">
      <BaseButton :to="{ name: 'home' }" variant="primary"> {{ $t('common.backToHome') }} </BaseButton>
      <BaseButton :to="{ name: 'events' }" variant="ghost"> {{ $t('features.register.success.viewEvents') }} </BaseButton>
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

.reg-success__verify-label {
  display: block;
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
  margin-bottom: 8px;
}

.reg-success__verify-row {
  display: flex;
  gap: 10px;
  align-items: center;
  flex-wrap: wrap;
}

.reg-success__verify-error {
  display: block;
  margin-top: 8px;
  color: var(--ca-danger);
  font-size: 13px;
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
