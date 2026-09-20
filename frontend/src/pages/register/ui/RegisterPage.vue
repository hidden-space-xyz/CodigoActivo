<script setup lang="ts">
import {
  AgeGate,
  RegistrationForm,
  RegistrationSuccess,
  useRegistration,
} from '@/features/register'
import { PageHeading } from '@/shared/ui'

const {
  step,
  form,
  submittedEmail,
  submittedMinorCount,
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
    <section class="register-head">
      <div class="ca-container">
        <PageHeading
          :title="$t('pages.register.title')"
          :description="$t('pages.register.intro')"
        />
      </div>
    </section>

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
.register-head {
  padding: 48px var(--ca-gutter) 20px;
}

.register-body {
  padding: 24px var(--ca-gutter) 80px;
}
</style>
