<script setup lang="ts">
import {
  AgeGate,
  RegistrationForm,
  RegistrationSuccess,
  useRegistration,
} from '@/features/register'
import { PageHead } from '@/shared/ui'

const {
  step,
  form,
  submittedEmail,
  submittedMinorCount,
  requiresVerification,
  resendCooldown,
  confirmAdult,
  backToGate,
  submit,
  resend,
  reset,
  isSubmitting,
  isResending,
} = useRegistration()
</script>

<template>
  <div>
    <PageHead :eyebrow="$t('pages.register.eyebrow')" :title="$t('pages.register.title')">
      <p class="register-head__intro">
        {{ $t('pages.register.intro') }}
      </p>
    </PageHead>

    <section class="register-body">
      <div class="ca-container--narrow">
        <AgeGate v-if="step === 'age-gate'" @confirm="confirmAdult" />

        <RegistrationForm
          v-else-if="step === 'form'"
          :form="form"
          :is-submitting="isSubmitting"
          @submit="submit"
          @back="backToGate"
        />

        <RegistrationSuccess
          v-else-if="step === 'success'"
          :minor-count="submittedMinorCount"
          :email="submittedEmail"
          :requires-verification="requiresVerification"
          :is-resending="isResending"
          :resend-cooldown="resendCooldown"
          @resend="resend"
          @reset="reset"
        />
      </div>
    </section>
  </div>
</template>

<style scoped>
.register-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 560px;
}

.register-body {
  padding: 24px var(--ca-gutter) 80px;
}
</style>
