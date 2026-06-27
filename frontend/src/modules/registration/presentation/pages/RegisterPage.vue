<script setup lang="ts">
import { useRegistration } from '@/modules/registration/presentation/composables/useRegistration'
import AgeGate from '@/modules/registration/presentation/components/AgeGate.vue'
import RegistrationForm from '@/modules/registration/presentation/components/RegistrationForm.vue'
import RegistrationSuccess from '@/modules/registration/presentation/components/RegistrationSuccess.vue'
import SectionEyebrow from '@/shared/ui/components/SectionEyebrow.vue'

const {
  step,
  form,
  adultRoles,
  minorRoles,
  submittedRoleName,
  submittedMinorCount,
  verificationCode,
  isVerified,
  confirmAdult,
  backToGate,
  submit,
  verify,
  reset,
  isSubmitting,
  isVerifying,
} = useRegistration()
</script>

<template>
  <div>
    <section class="register-head">
      <div class="register-head__glow" aria-hidden="true" />
      <div class="ca-container--narrow register-head__inner">
        <SectionEyebrow text="// regístrate" color="var(--ca-cyan)" />
        <h1 class="register-head__title">Únete a Código Activo</h1>
        <p class="register-head__intro">
          Regístrate y, si tienes menores a tu cargo, inscríbelos a todos en un solo paso.
        </p>
      </div>
    </section>

    <section class="register-body">
      <div class="ca-container--narrow">
        <AgeGate v-if="step === 'age-gate'" @confirm="confirmAdult" />

        <RegistrationForm
          v-else-if="step === 'form'"
          :form="form"
          :adult-roles="adultRoles.data.value ?? []"
          :minor-roles="minorRoles.data.value ?? []"
          :is-submitting="isSubmitting"
          @submit="submit"
          @back="backToGate"
        />

        <RegistrationSuccess
          v-else-if="step === 'success'"
          :role-name="submittedRoleName"
          :minor-count="submittedMinorCount"
          :verification-code="verificationCode"
          :is-verified="isVerified"
          :is-verifying="isVerifying"
          @verify="verify"
          @reset="reset"
        />
      </div>
    </section>
  </div>
</template>

<style scoped>
.register-head {
  position: relative;
  overflow: hidden;
  padding: 64px 24px 16px;
}

.register-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, rgba(45, 212, 217, 0.1), transparent 60%);
}

.register-head__inner {
  position: relative;
}

.register-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.register-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 560px;
}

.register-body {
  padding: 24px 24px 80px;
}
</style>
