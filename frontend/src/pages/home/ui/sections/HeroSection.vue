<script setup lang="ts">
import { useI18n } from 'vue-i18n'

import RobotGameSection from './RobotGameSection.vue'
import { FOUNDING_YEAR } from '@/shared/config'
import { BaseButton } from '@/shared/ui'

const { t } = useI18n()

const stats = [
  { value: '8–18', label: t('pages.home.hero.stats.years'), color: 'var(--ca-orange-ink)' },
  { value: '100%', label: t('pages.home.hero.stats.free'), color: 'var(--ca-success-ink)' },
  { value: '+15', label: t('pages.home.hero.stats.events'), color: 'var(--ca-azure-ink)' },
] as const
</script>

<template>
  <section class="hero">
    <div class="ca-container hero__grid">
      <div>
        <div class="hero__badge">● {{ $t('pages.home.hero.badge', { year: FOUNDING_YEAR }) }}</div>
        <h1 class="hero__title">
          {{ $t('pages.home.hero.titleLine1') }}<br />{{ $t('pages.home.hero.titleLine2') }}
          <span style="color: var(--ca-orange)">{{ $t('pages.home.hero.titleHighlight') }}</span>.
        </h1>
        <p class="hero__subtitle">{{ $t('pages.home.hero.subtitle') }}</p>

        <div class="hero__actions">
          <BaseButton :to="{ name: 'register' }" variant="primary">
            {{ $t('common.register') }}
          </BaseButton>
          <BaseButton :to="{ name: 'about' }" variant="ghost">
            {{ $t('pages.home.hero.aboutCta') }}
          </BaseButton>
        </div>

        <div class="hero__stats">
          <div v-for="stat in stats" :key="stat.label" class="hero__stat">
            <div class="hero__stat-value" :style="{ color: stat.color }">{{ stat.value }}</div>
            <div class="hero__stat-label">{{ stat.label }}</div>
          </div>
        </div>
      </div>

      <RobotGameSection />
    </div>
  </section>
</template>

<style scoped>
.hero {
  position: relative;
  overflow: hidden;
  padding: 84px 24px 64px;
}

.hero__grid {
  position: relative;
  display: grid;
  grid-template-columns: 1.05fr 0.95fr;
  gap: 56px;
  align-items: center;
}

.hero__badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-family: var(--ca-font-mono);
  font-size: 12px;
  font-weight: 500;
  letter-spacing: 0.08em;
  color: var(--ca-lime-ink);
  background: var(--ca-lime-soft);
  padding: 6px 12px;
  border-radius: 999px;
  margin-bottom: 22px;
}

.hero__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 60px;
  line-height: 1.02;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.hero__subtitle {
  margin-top: 22px;
  font-size: 19px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 480px;
}

.hero__actions {
  display: flex;
  gap: 14px;
  margin-top: 32px;
  flex-wrap: wrap;
}

.hero__stats {
  display: flex;
  gap: 30px;
  margin-top: 40px;
}

.hero__stat-value {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 26px;
}

.hero__stat-label {
  font-size: 13px;
  color: var(--ca-text-faint);
}

@media (max-width: 860px) {
  .hero__grid {
    grid-template-columns: 1fr;
    gap: 36px;
  }
  .hero__title {
    font-size: 44px;
  }
}
</style>
