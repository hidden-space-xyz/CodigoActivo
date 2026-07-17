<script setup lang="ts">
import { ref } from 'vue'

import { BaseButton } from '@/shared/ui'

const emit = defineEmits<{ confirm: [] }>()

const declined = ref(false)
</script>

<template>
  <div class="age-gate">
    <template v-if="!declined">
      <h2 class="age-gate__heading">{{ $t('features.register.ageGate.heading') }}</h2>
      <p class="age-gate__lead">
        {{ $t('features.register.ageGate.lead') }}
      </p>
      <p class="age-gate__question">{{ $t('features.register.ageGate.question') }}</p>
      <div class="age-gate__actions">
        <BaseButton variant="primary" @click="emit('confirm')">{{
          $t('features.register.ageGate.confirm')
        }}</BaseButton>
        <BaseButton variant="ghost" @click="declined = true">{{
          $t('features.register.ageGate.decline')
        }}</BaseButton>
      </div>
    </template>

    <div v-else class="age-gate__blocked">
      <div class="age-gate__blocked-icon" aria-hidden="true">🔒</div>
      <h2 class="age-gate__heading">{{ $t('features.register.ageGate.blockedHeading') }}</h2>
      <p class="age-gate__lead">
        {{ $t('features.register.ageGate.blockedLead') }}
      </p>
      <BaseButton variant="link" @click="declined = false">{{
        $t('features.register.back')
      }}</BaseButton>
    </div>
  </div>
</template>

<style scoped>
.age-gate {
  max-width: 560px;
  margin: 0 auto;
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 20px;
  padding: 40px 36px;
  text-align: center;
}

.age-gate__heading {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 26px;
  letter-spacing: -0.02em;
  color: var(--ca-text-bright);
}

.age-gate__lead {
  margin-top: 14px;
  font-size: 16px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.age-gate__question {
  margin-top: 24px;
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 20px;
  color: var(--ca-text);
}

.age-gate__actions {
  display: flex;
  gap: 14px;
  justify-content: center;
  flex-wrap: wrap;
  margin-top: 20px;
}

.age-gate__blocked-icon {
  font-size: 40px;
  margin-bottom: 8px;
}
</style>
