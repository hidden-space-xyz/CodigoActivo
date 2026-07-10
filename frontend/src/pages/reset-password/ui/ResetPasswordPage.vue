<script setup lang="ts">
import Password from 'primevue/password'
import { useRoute } from 'vue-router'

import { useResetPassword } from '@/features/auth'
import { BaseButton, SectionEyebrow } from '@/shared/ui'

const route = useRoute()

function queryString(value: unknown): string | null {
  if (Array.isArray(value)) value = value[0]
  return typeof value === 'string' && value.length > 0 ? value : null
}

const { form, state, errorMessage, canRequestNewLink, submit, hasValidLink, isSubmitting } =
  useResetPassword(queryString(route.query.userId), queryString(route.query.code))
</script>

<template>
  <div>
    <section class="reset-head">
      <div class="reset-head__glow" aria-hidden="true" />
      <div class="ca-container--narrow reset-head__inner">
        <SectionEyebrow text="// contraseña" color="var(--ca-orange-ink)" />
        <h1 class="reset-head__title">Elige una nueva contraseña</h1>
      </div>
    </section>

    <section class="reset-body">
      <div class="reset-card">
        <div v-if="!hasValidLink" class="reset-panel">
          <div class="reset-panel__icon reset-panel__icon--error" aria-hidden="true">!</div>
          <h2 class="reset-panel__title">El enlace no es válido</h2>
          <p class="reset-panel__text" role="alert">
            El enlace para restablecer la contraseña no es válido o está incompleto. Solicita uno
            nuevo y te lo enviaremos por correo.
          </p>
          <div class="reset-panel__actions">
            <BaseButton :to="{ name: 'forgot-password' }" variant="primary">
              Solicitar un enlace nuevo
            </BaseButton>
            <BaseButton :to="{ name: 'home' }" variant="ghost">Volver al inicio</BaseButton>
          </div>
        </div>

        <div v-else-if="state === 'success'" class="reset-panel">
          <div class="reset-panel__icon reset-panel__icon--ok" aria-hidden="true">✓</div>
          <h2 class="reset-panel__title">¡Contraseña actualizada!</h2>
          <p class="reset-panel__text">
            Tu contraseña se ha cambiado correctamente. Ya puedes iniciar sesión con ella.
          </p>
          <div class="reset-panel__actions">
            <BaseButton :to="{ name: 'login' }" variant="primary">Iniciar sesión</BaseButton>
          </div>
        </div>

        <form v-else class="reset-form" @submit.prevent="submit">
          <div class="reset-field">
            <label class="reset-label" for="reset-password">Nueva contraseña</label>
            <Password
              input-id="reset-password"
              v-model="form.password"
              :feedback="false"
              toggle-mask
              :maxlength="128"
              required
              fluid
            />
          </div>
          <div class="reset-field">
            <label class="reset-label" for="reset-password-confirm">Repite la contraseña</label>
            <Password
              input-id="reset-password-confirm"
              v-model="form.confirmPassword"
              :feedback="false"
              toggle-mask
              :maxlength="128"
              required
              fluid
            />
          </div>

          <p v-if="errorMessage" class="reset-error" role="alert">{{ errorMessage }}</p>
          <RouterLink
            v-if="canRequestNewLink"
            :to="{ name: 'forgot-password' }"
            class="reset-request-link"
          >
            Solicitar un enlace nuevo
          </RouterLink>

          <BaseButton type="submit" variant="primary" block :loading="isSubmitting">
            Cambiar contraseña
          </BaseButton>
        </form>
      </div>
    </section>
  </div>
</template>

<style scoped>
.reset-head {
  position: relative;
  overflow: hidden;
  padding: 64px 24px 16px;
}

.reset-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-orange-soft), transparent 60%);
}

.reset-head__inner {
  position: relative;
}

.reset-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.reset-body {
  padding: 24px 24px 80px;
}

.reset-card {
  max-width: 440px;
  margin: 0 auto;
}

.reset-form {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.reset-field {
  display: flex;
  flex-direction: column;
}

.reset-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
  margin-bottom: 6px;
}

.reset-error {
  font-size: 13.5px;
  color: var(--ca-danger-ink);
}

.reset-request-link {
  margin-top: -8px;
  font-size: 13.5px;
  font-weight: 600;
  color: var(--ca-orange-ink);
  text-decoration: none;
}

.reset-request-link:hover {
  text-decoration: underline;
}

.reset-panel {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 36px 30px;
  text-align: center;
}

.reset-panel__icon {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  margin: 0 auto 20px;
}

.reset-panel__icon--ok {
  background: var(--ca-success-soft);
  border: 1px solid var(--ca-success);
  color: var(--ca-success);
}

.reset-panel__icon--error {
  background: var(--ca-danger-soft);
  border: 1px solid var(--ca-danger);
  color: var(--ca-danger);
  font-weight: 700;
}

.reset-panel__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 24px;
  color: var(--ca-text-bright);
}

.reset-panel__text {
  margin-top: 10px;
  font-size: 15.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.reset-panel__actions {
  display: flex;
  gap: 14px;
  justify-content: center;
  margin-top: 24px;
  flex-wrap: wrap;
}
</style>
