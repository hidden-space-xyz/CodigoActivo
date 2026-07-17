<script setup lang="ts">
import { BaseButton, SectionEyebrow } from '@/shared/ui'
</script>

<template>
  <div>
    <section class="not-found">
      <div class="not-found__glow" aria-hidden="true" />
      <div class="ca-container--narrow not-found__inner">
        <SectionEyebrow :text="$t('pages.notFound.eyebrow')" color="var(--ca-orange-ink)" />
        <h1 class="not-found__title">{{ $t('pages.notFound.title') }}</h1>
        <p class="not-found__text">
          {{ $t('pages.notFound.text') }}
        </p>
        <div class="not-found__actions">
          <BaseButton variant="primary" :to="{ name: 'home' }">{{
            $t('common.backToHome')
          }}</BaseButton>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.not-found {
  position: relative;
  overflow: hidden;
  padding: 64px 24px 80px;
}

.not-found__glow {
  position: absolute;
  inset: 0;
  background: radial-gradient(700px 400px at 80% -20%, var(--ca-orange-soft), transparent 60%);
}

.not-found__inner {
  position: relative;
}

.not-found__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.not-found__text {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 520px;
}

.not-found__actions {
  margin-top: 28px;
}
</style>
