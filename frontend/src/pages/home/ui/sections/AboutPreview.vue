<script setup lang="ts">
import { useOrganizationContent, ValueCard } from '@/entities/organization'
import { BaseButton, SectionEyebrow } from '@/shared/ui'

const { values } = useOrganizationContent()
</script>

<template>
  <section class="about-preview">
    <div class="ca-container about-preview__grid">
      <div>
        <SectionEyebrow :text="$t('pages.home.about.eyebrow')" color="var(--ca-azure-ink)" />
        <h2 class="about-preview__title">{{ $t('pages.home.about.title') }}</h2>
        <p class="about-preview__text">{{ $t('pages.home.about.body') }}</p>
        <BaseButton :to="{ name: 'about' }" variant="purple" class="about-preview__cta">
          {{ $t('pages.home.about.cta') }}
        </BaseButton>
      </div>

      <div class="about-preview__values">
        <ValueCard v-for="value in values" :key="value.id" :value="value" size="sm" />
      </div>
    </div>
  </section>
</template>

<style scoped>
.about-preview {
  padding: 56px 24px;
}

.about-preview__grid {
  background: var(--ca-surface-2);
  border: 1px solid var(--ca-border);
  border-radius: 20px;
  padding: 40px;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 40px;
  align-items: center;
}

.about-preview__title {
  font-family: var(--ca-font-display);
  font-size: 32px;
  font-weight: 700;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
  line-height: 1.1;
}

.about-preview__text {
  margin-top: 16px;
  font-size: 16px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.about-preview__cta {
  margin-top: 22px;
  font-size: 15px;
  padding: 12px 22px;
  border-radius: 11px;
}

.about-preview__values {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
}

@media (max-width: 860px) {
  .about-preview__grid {
    grid-template-columns: 1fr;
    padding: 28px;
  }
}
</style>
