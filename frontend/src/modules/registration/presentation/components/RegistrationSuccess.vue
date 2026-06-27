<script setup lang="ts">
import { computed, ref } from 'vue'
import InputText from 'primevue/inputtext'

import {
  ROLE_LABELS,
  roleRemindsAboutEvents,
  type RegistrationRole,
} from '@/modules/registration/domain/value-objects/registration-role'
import BaseButton from '@/shared/ui/components/BaseButton.vue'

const props = defineProps<{
  role: RegistrationRole
  verificationCode: string | null
  isVerified: boolean
  isVerifying: boolean
}>()
const emit = defineEmits<{ reset: []; verify: [otp: string] }>()

const roleTitle = computed(() => ROLE_LABELS[props.role])
const remindsEvents = computed(() => roleRemindsAboutEvents(props.role))

const otp = ref(props.verificationCode ?? '')

function submitVerify(): void {
  if (otp.value.trim()) emit('verify', otp.value.trim())
}
</script>

<template>
  <div class="reg-success">
    <div class="reg-success__check" aria-hidden="true">✓</div>
    <h2 class="reg-success__title">¡Registro completado!</h2>
    <p class="reg-success__role">
      Te has registrado como <b>{{ roleTitle }}</b
      >.
    </p>

    <p class="reg-success__thanks">
      Te agradecemos que quieras colaborar con nosotros. Nos pondremos en contacto contigo lo antes
      posible para conocernos más.
    </p>
    <p v-if="remindsEvents" class="reg-success__reminder">
      Recuerda que puedes inscribirte al evento que quieras desde la sección de eventos.
    </p>

    <div v-if="isVerified" class="reg-success__verified">Tu cuenta ha sido verificada.</div>
    <form v-else class="reg-success__verify" @submit.prevent="submitVerify">
      <label class="reg-success__verify-label" for="reg-otp"
        >Verifica tu cuenta con el código</label
      >
      <div class="reg-success__verify-row">
        <InputText id="reg-otp" v-model="otp" placeholder="Código de verificación" fluid />
        <BaseButton type="submit" variant="primary" :loading="isVerifying">Verificar</BaseButton>
      </div>
    </form>

    <div class="reg-success__actions">
      <BaseButton :to="{ name: 'home' }" variant="primary"> Volver al inicio </BaseButton>
      <BaseButton v-if="remindsEvents" :to="{ name: 'events' }" variant="ghost">
        Ver eventos
      </BaseButton>
    </div>

    <BaseButton variant="link" class="reg-success__again" @click="emit('reset')">
      Registrar a otra persona
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
  background: rgba(91, 229, 132, 0.15);
  border: 1px solid var(--ca-green);
  color: var(--ca-green);
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
  color: #c4cad6;
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
  align-items: stretch;
}

.reg-success__verified {
  margin-top: 22px;
  background: rgba(91, 229, 132, 0.12);
  border: 1px solid var(--ca-green);
  border-radius: 12px;
  padding: 14px;
  color: var(--ca-text);
  font-size: 14.5px;
}
</style>
