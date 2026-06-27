<script setup lang="ts">
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'

import { useLogin } from '@/modules/auth/presentation/composables/useLogin'
import BaseButton from '@/shared/ui/components/BaseButton.vue'
import SectionEyebrow from '@/shared/ui/components/SectionEyebrow.vue'

const { form, submit, isSubmitting, isError } = useLogin()
</script>

<template>
  <div>
    <section class="login-head">
      <div class="login-head__glow" aria-hidden="true" />
      <div class="ca-container--narrow login-head__inner">
        <SectionEyebrow text="// iniciar sesión" color="var(--ca-cyan)" />
        <h1 class="login-head__title">Bienvenido de nuevo</h1>
        <p class="login-head__intro">
          Inicia sesión en tu cuenta para participar en nuestras actividades.
        </p>
      </div>
    </section>

    <section class="login-body">
      <div class="login-card">
        <form class="login-form" @submit.prevent="submit">
          <div class="login-field">
            <label class="login-label" for="login-identifier">Correo o usuario</label>
            <InputText id="login-identifier" v-model="form.identifier" required fluid />
          </div>
          <div class="login-field">
            <label class="login-label" for="login-password">Contraseña</label>
            <Password
              input-id="login-password"
              v-model="form.password"
              :feedback="false"
              toggle-mask
              required
              fluid
            />
          </div>

          <p v-if="isError" class="login-error" role="alert">
            No hemos podido iniciar sesión. Revisa tus datos e inténtalo de nuevo.
          </p>

          <BaseButton type="submit" variant="primary" block :loading="isSubmitting">
            Iniciar sesión
          </BaseButton>
        </form>

        <p class="login-alt">
          ¿Aún no tienes cuenta?
          <RouterLink :to="{ name: 'register' }" class="login-alt__link"> Regístrate </RouterLink>
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
  background: radial-gradient(700px 400px at 80% -20%, rgba(45, 212, 217, 0.1), transparent 60%);
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

.login-error {
  font-size: 13.5px;
  color: var(--ca-coral);
}

.login-alt {
  text-align: center;
  margin-top: 18px;
  font-size: 14.5px;
  color: var(--ca-text-muted);
}

.login-alt__link {
  color: var(--ca-cyan);
  font-weight: 600;
  text-decoration: none;
}

.login-alt__link:hover {
  text-decoration: underline;
}
</style>
