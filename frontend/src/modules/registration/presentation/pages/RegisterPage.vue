<script setup lang="ts">
import { useRegistration } from '@/modules/registration/presentation/composables/useRegistration'
import RegistrationForm from '@/modules/registration/presentation/components/RegistrationForm.vue'
import RegistrationSuccess from '@/modules/registration/presentation/components/RegistrationSuccess.vue'
import RoleSelector from '@/modules/registration/presentation/components/RoleSelector.vue'
import SectionEyebrow from '@/shared/ui/components/SectionEyebrow.vue'

const {
  step,
  form,
  isUserMinor,
  submittedRole,
  verificationCode,
  isVerified,
  selectRole,
  goBackToRoles,
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
          Cualquier persona puede registrarse. Primero, dinos cómo quieres participar.
        </p>
      </div>
    </section>

    <section class="register-body">
      <div class="ca-container--narrow">
        <RoleSelector v-if="step === 'role'" @select="selectRole" />

        <RegistrationForm
          v-else-if="step === 'form'"
          :form="form"
          :is-user-minor="isUserMinor"
          :is-submitting="isSubmitting"
          @submit="submit"
          @back="goBackToRoles"
        />

        <RegistrationSuccess
          v-else-if="step === 'success' && submittedRole"
          :role="submittedRole"
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
