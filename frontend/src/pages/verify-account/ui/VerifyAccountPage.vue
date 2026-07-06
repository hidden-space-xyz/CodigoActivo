<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'

import { useAccountVerification } from '@/features/register'
import { BaseButton, SectionEyebrow } from '@/shared/ui'

const route = useRoute()
const { state, errorMessage, verify, resend, canResend, isResending } = useAccountVerification()

function queryString(value: unknown): string | null {
  if (Array.isArray(value)) value = value[0]
  return typeof value === 'string' && value.length > 0 ? value : null
}

onMounted(() => {
  verify(queryString(route.query.userId), queryString(route.query.code))
})
</script>

<template>
  <div>
    <section class="verify-head">
      <div class="verify-head__glow" aria-hidden="true" />
      <div class="ca-container--narrow verify-head__inner">
        <SectionEyebrow text="// verificación" color="var(--ca-orange-ink)" />
        <h1 class="verify-head__title">Verifica tu cuenta</h1>
      </div>
    </section>

    <section class="verify-body">
      <div class="ca-container--narrow">
        <div class="verify-card" :class="`verify-card--${state}`">
          <template v-if="state === 'verifying'">
            <i class="pi pi-spin pi-spinner verify-card__icon" aria-hidden="true" />
            <p class="verify-card__text" aria-live="polite">Estamos verificando tu cuenta…</p>
          </template>

          <template v-else-if="state === 'success'">
            <div class="verify-card__icon verify-card__icon--ok" aria-hidden="true">✓</div>
            <h2 class="verify-card__title">¡Cuenta verificada!</h2>
            <p class="verify-card__text">
              Tu cuenta ha sido activada correctamente. Ya puedes iniciar sesión.
            </p>
            <BaseButton :to="{ name: 'login' }" variant="primary">Iniciar sesión</BaseButton>
          </template>

          <template v-else>
            <div class="verify-card__icon verify-card__icon--error" aria-hidden="true">!</div>
            <h2 class="verify-card__title">No hemos podido verificar tu cuenta</h2>
            <p class="verify-card__text" role="alert">
              {{ errorMessage ?? 'El enlace de verificación no es válido o ha caducado.' }}
            </p>
            <p class="verify-card__hint">
              Si el enlace ha caducado, pídenos un enlace nuevo y te lo enviaremos por correo.
            </p>
            <div class="verify-card__actions">
              <BaseButton
                v-if="canResend"
                variant="primary"
                :loading="isResending"
                @click="resend"
              >
                Enviarme un enlace nuevo
              </BaseButton>
              <BaseButton :to="{ name: 'home' }" variant="ghost">Volver al inicio</BaseButton>
            </div>
          </template>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.verify-head {
  position: relative;
  overflow: hidden;
  padding: 64px 24px 16px;
}

.verify-head__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-orange-soft), transparent 60%);
}

.verify-head__inner {
  position: relative;
}

.verify-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.verify-body {
  padding: 24px 24px 80px;
}

.verify-card {
  max-width: 540px;
  margin: 0 auto;
  text-align: center;
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 20px;
  padding: 44px 36px;
}

.verify-card__icon {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  margin: 0 auto 20px;
}

.verify-card__icon--ok {
  background: var(--ca-success-soft);
  border: 1px solid var(--ca-success);
  color: var(--ca-success);
}

.verify-card__icon--error {
  background: var(--ca-danger-soft);
  border: 1px solid var(--ca-danger);
  color: var(--ca-danger);
  font-weight: 700;
}

.verify-card__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 24px;
  color: var(--ca-text-bright);
}

.verify-card__text {
  margin-top: 10px;
  font-size: 15.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.verify-card__hint {
  margin-top: 12px;
  font-size: 14px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.verify-card__actions {
  display: flex;
  gap: 14px;
  justify-content: center;
  margin-top: 24px;
  flex-wrap: wrap;
}

.verify-card--success :deep(.base-button),
.verify-card--verifying {
  margin-top: 0;
}

.verify-card--success .base-button {
  margin-top: 24px;
}
</style>
