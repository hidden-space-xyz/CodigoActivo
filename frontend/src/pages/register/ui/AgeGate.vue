<script setup lang="ts">
import { ref } from 'vue'
import { BrandButton } from '@/shared/ui/brand-button'

const emit = defineEmits<{
  /** Fired when the visitor confirms being an adult; declining is handled inside the component. */
  confirm: []
}>()

const declined = ref(false)
</script>

<template>
  <div class="age-gate">
    <template v-if="!declined">
      <h2 class="age-gate__heading">{{ $t('pages.register.ageGate.heading') }}</h2>
      <p class="age-gate__lead">
        {{ $t('pages.register.ageGate.lead') }}
      </p>
      <p class="age-gate__question">{{ $t('pages.register.ageGate.question') }}</p>
      <div class="age-gate__actions">
        <BrandButton variant="primary" @click="emit('confirm')">{{
          $t('pages.register.ageGate.confirm')
        }}</BrandButton>
        <BrandButton variant="ghost" @click="declined = true">{{
          $t('pages.register.ageGate.decline')
        }}</BrandButton>
      </div>
    </template>

    <div v-else class="age-gate__blocked">
      <div class="age-gate__blocked-icon" aria-hidden="true">🔒</div>
      <h2 class="age-gate__heading">{{ $t('pages.register.ageGate.blockedHeading') }}</h2>
      <p class="age-gate__lead">
        {{ $t('pages.register.ageGate.blockedLead') }}
      </p>
      <BrandButton variant="back" @click="declined = false">{{
        $t('pages.register.back')
      }}</BrandButton>
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
