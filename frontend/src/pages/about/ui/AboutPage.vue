<script setup lang="ts">
import { ref } from 'vue'
import Dialog from 'primevue/dialog'

import { ActivityStep, useOrganizationContent, ValueCard } from '@/entities/organization'
import { CONTACT } from '@/shared/config'
import { BaseButton, SectionEyebrow } from '@/shared/ui'

const { values, activities } = useOrganizationContent()

const joinVisible = ref(false)
</script>

<template>
  <div>
    <section class="about-hero">
      <div class="about-hero__glow" aria-hidden="true" />
      <div class="about-hero__inner">
        <SectionEyebrow :text="$t('pages.about.eyebrowUs')" color="var(--ca-azure-ink)" />
        <h1 class="about-hero__title">
          {{ $t('pages.about.hero.titleLine1') }}<br />{{ $t('pages.about.hero.titleLine2') }}
          <span style="color: var(--ca-azure)">{{ $t('pages.about.hero.titleHighlight') }}</span>
        </h1>
        <p class="about-hero__lead">
          {{ $t('pages.about.hero.lead') }}
        </p>
      </div>
    </section>

    <section class="about-values">
      <div class="ca-container--wide about-values__grid">
        <ValueCard v-for="value in values" :key="value.id" :value="value" />
      </div>
    </section>

    <section class="about-what">
      <div class="ca-container--wide about-what__grid">
        <div class="about-what__photo" aria-hidden="true">
          foto del equipo / taller en acción 1200 × 900
        </div>
        <div>
          <SectionEyebrow :text="$t('pages.about.eyebrowWhat')" color="var(--ca-lime-ink)" />
          <h2 class="about-what__title">{{ $t('pages.about.what.title') }}</h2>
          <div class="about-what__list">
            <ActivityStep v-for="activity in activities" :key="activity.id" :activity="activity" />
          </div>
        </div>
      </div>
    </section>

    <section class="about-cta">
      <div class="ca-container--wide about-cta__card">
        <h2 class="about-cta__title">{{ $t('pages.about.cta.title') }}</h2>
        <p class="about-cta__text">
          {{ $t('pages.about.cta.text') }}
        </p>
        <div class="about-cta__actions">
          <BaseButton variant="primary" @click="joinVisible = true">
            {{ $t('pages.about.cta.join') }}
          </BaseButton>
        </div>
      </div>
    </section>

    <Dialog
      v-model:visible="joinVisible"
      modal
      :draggable="false"
      :header="$t('pages.about.cta.dialog.header')"
      :style="{ width: '90vw', maxWidth: '480px' }"
    >
      <p class="join__lead">{{ $t('pages.about.cta.dialog.lead') }}</p>

      <ul class="join__contact">
        <li class="join__item">
          <i class="pi pi-envelope" aria-hidden="true" />
          <span class="join__label">{{ $t('common.emailLong') }}</span>
          <a :href="`mailto:${CONTACT.email}`" class="join__value">{{ CONTACT.email }}</a>
        </li>
        <li class="join__item">
          <i class="pi pi-phone" aria-hidden="true" />
          <span class="join__label">{{ $t('common.phone') }}</span>
          <a :href="`tel:${CONTACT.phoneHref}`" class="join__value">{{ CONTACT.phone }}</a>
        </li>
      </ul>

      <p class="join__note">{{ $t('pages.about.cta.dialog.note') }}</p>
    </Dialog>
  </div>
</template>

<style scoped>
.about-hero {
  padding: 72px 24px 40px;
  position: relative;
  overflow: hidden;
}

.about-hero__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-azure-soft), transparent 60%);
}

.about-hero__inner {
  position: relative;
  max-width: 900px;
  margin: 0 auto;
  text-align: center;
}

.about-hero__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 48px;
  line-height: 1.05;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.about-hero__lead {
  margin: 22px auto 0;
  font-size: 18px;
  line-height: 1.65;
  color: var(--ca-text-muted);
  max-width: 660px;
}

.about-values {
  padding: 32px 24px;
}

.about-values__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 18px;
}

.about-what {
  padding: 48px 24px;
}

.about-what__grid {
  display: grid;
  grid-template-columns: 0.9fr 1.1fr;
  gap: 48px;
  align-items: center;
}

.about-what__photo {
  min-height: 340px;
  border-radius: 18px;
  background: repeating-linear-gradient(
    135deg,
    var(--ca-border),
    var(--ca-border) 11px,
    transparent 11px,
    transparent 22px
  );
  border: 1px solid var(--ca-border);
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: var(--ca-font-mono);
  color: var(--ca-text-ghost);
  font-size: 13px;
  text-align: center;
}

.about-what__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 32px;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}

.about-what__list {
  margin-top: 22px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.about-cta {
  padding: 32px 24px 72px;
}

.about-cta__card {
  background: linear-gradient(120deg, var(--ca-orange-soft), var(--ca-azure-soft));
  border: 1px solid var(--ca-border-strong);
  border-radius: 20px;
  padding: 44px;
  text-align: center;
}

.about-cta__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 30px;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}

.about-cta__text {
  margin: 14px auto 0;
  font-size: 16px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 540px;
}

.about-cta__actions {
  display: flex;
  gap: 14px;
  justify-content: center;
  margin-top: 26px;
  flex-wrap: wrap;
}

.join__lead {
  margin: 0;
  font-size: 15px;
  line-height: 1.65;
  color: var(--ca-text-muted);
}

.join__contact {
  list-style: none;
  margin: 20px 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.join__item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  border: 1px solid var(--ca-border-strong);
  border-radius: 12px;
  background: var(--ca-surface-2);
}

.join__item .pi {
  font-size: 15px;
  color: var(--ca-orange-ink);
}

.join__label {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--ca-text-muted);
}

.join__value {
  margin-left: auto;
  font-weight: 600;
  color: var(--ca-text-bright);
  text-decoration: none;
}

.join__value:hover {
  color: var(--ca-orange-ink);
  text-decoration: underline;
}

.join__note {
  margin: 18px 0 0;
  font-size: 14px;
  color: var(--ca-text-dim);
}

@media (max-width: 480px) {
  .join__item {
    flex-wrap: wrap;
  }
  .join__value {
    margin-left: 0;
    flex-basis: 100%;
  }
}

@media (max-width: 860px) {
  .about-what__grid {
    grid-template-columns: 1fr;
    gap: 28px;
  }
  .about-hero__title {
    font-size: 36px;
  }
}
</style>
